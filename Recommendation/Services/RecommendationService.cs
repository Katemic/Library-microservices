using System.Text;
using System.Text.Json;
using Recommendation.Persistence;

namespace Recommendation.Services
{
    public sealed record RecommendedItem(
        int ItemId,
        string Name,
        string? MediaType,
        string Reason);

    public sealed record RecommendationResult(
        int LoanerId,
        // "llm" when Ollama ranked the items, "graph" when we fell back
        // to the Neo4j score (Ollama down, timed out or gave bad output).
        string Source,
        List<RecommendedItem> Recommendations);

    /// <summary>
    /// 1. Neo4j picks real candidate items for the loaner.
    /// 2. Ollama ranks them and writes a short reason for each.
    /// 3. Any item id the LLM invents is dropped, so only real items come back.
    /// </summary>
    public sealed class RecommendationService
    {
        private const int CandidatePoolSize = 20;
        private const int HistorySize = 15;

        private const string SystemPrompt =
            """
            You are a library recommendation assistant.
            You will get a loaner's borrowing history and a list of candidate items.
            Pick the best candidates for this loaner and explain each pick in one short sentence.
            Only choose items from the candidate list, using their exact id.
            Reply with JSON only, in this shape:
            {"recommendations": [{"itemId": 123, "reason": "..."}]}
            """;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly RecommendationQueries _queries;
        private readonly OllamaClient _ollama;
        private readonly ILogger<RecommendationService> _logger;

        public RecommendationService(
            RecommendationQueries queries,
            OllamaClient ollama,
            ILogger<RecommendationService> logger)
        {
            _queries = queries;
            _ollama = ollama;
            _logger = logger;
        }

        public async Task<RecommendationResult> RecommendAsync(
            int loanerId,
            int count,
            CancellationToken cancellationToken)
        {
            var history = await _queries.GetLoanHistoryAsync(loanerId, HistorySize);

            var personalised = history.Count > 0;
            var candidates = personalised
                ? await _queries.GetPersonalisedCandidatesAsync(loanerId, CandidatePoolSize)
                : [];

            // No history, or nothing related found: suggest popular items instead.
            if (candidates.Count == 0)
            {
                personalised = false;
                candidates = await _queries.GetPopularCandidatesAsync(loanerId, CandidatePoolSize);
            }

            if (candidates.Count == 0)
            {
                return new RecommendationResult(loanerId, "graph", []);
            }

            try
            {
                var picks = await AskLlmAsync(history, candidates, count, cancellationToken);

                if (picks.Count > 0)
                {
                    return new RecommendationResult(loanerId, "llm", picks);
                }

                _logger.LogWarning(
                    "LLM returned no valid items for loaner {LoanerId}; using graph ranking",
                    loanerId);
            }
            catch (Exception ex) when (
                (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
                && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex, "Ollama unavailable for loaner {LoanerId}; using graph ranking",
                    loanerId);
            }

            var fallbackReason = personalised
                ? "Similar to items you have borrowed"
                : "Popular with other loaners";

            return new RecommendationResult(
                loanerId,
                "graph",
                candidates
                    .Take(count)
                    .Select(c => new RecommendedItem(c.Id, c.Name, c.MediaType, fallbackReason))
                    .ToList());
        }

        private async Task<List<RecommendedItem>> AskLlmAsync(
            List<ItemSummary> history,
            List<ItemSummary> candidates,
            int count,
            CancellationToken cancellationToken)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("Borrowing history:");
            if (history.Count == 0)
            {
                prompt.AppendLine("- (none yet, this is a new loaner)");
            }
            foreach (var item in history)
            {
                prompt.AppendLine($"- {Describe(item)}");
            }

            prompt.AppendLine();
            prompt.AppendLine("Candidates:");
            foreach (var item in candidates)
            {
                prompt.AppendLine($"- id {item.Id}: {Describe(item)}");
            }

            prompt.AppendLine();
            prompt.AppendLine($"Pick up to {count} candidates.");

            var reply = await _ollama.ChatJsonAsync(
                SystemPrompt, prompt.ToString(), cancellationToken);

            var parsed = JsonSerializer.Deserialize<LlmReply>(reply, JsonOptions);
            var byId = candidates.ToDictionary(c => c.Id);

            // Keep only ids that really were candidates; drop duplicates.
            return (parsed?.Recommendations ?? [])
                .Where(p => byId.ContainsKey(p.ItemId))
                .DistinctBy(p => p.ItemId)
                .Take(count)
                .Select(p =>
                {
                    var item = byId[p.ItemId];
                    var reason = string.IsNullOrWhiteSpace(p.Reason)
                        ? "Recommended for you"
                        : p.Reason.Trim();

                    return new RecommendedItem(item.Id, item.Name, item.MediaType, reason);
                })
                .ToList();
        }

        private static string Describe(ItemSummary item)
        {
            var parts = new List<string> { $"\"{item.Name}\"" };

            if (item.MediaType != null) parts.Add(item.MediaType);
            if (item.ReleaseYear != null) parts.Add(item.ReleaseYear.ToString()!);
            if (item.Creators.Count > 0) parts.Add($"by {string.Join(", ", item.Creators)}");
            if (item.Genres.Count > 0) parts.Add($"genres: {string.Join(", ", item.Genres)}");
            if (item.Tags.Count > 0) parts.Add($"tags: {string.Join(", ", item.Tags)}");

            return string.Join(" | ", parts);
        }

        private sealed record LlmReply(List<LlmPick>? Recommendations);

        private sealed record LlmPick(int ItemId, string? Reason);
    }
}
