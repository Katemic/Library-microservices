using System.Text.Json;
using Neo4j.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Recommendation.Contracts;
using Recommendation.Persistence;

namespace Recommendation.Consumers;

public sealed class RecommendationConsumer : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly GraphRepository _repository;
    private readonly ILogger<RecommendationConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public RecommendationConsumer(
        IConfiguration config,
        GraphRepository repository,
        ILogger<RecommendationConsumer> logger)
    {
        _config = config;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "rabbitmq",
            Port = _config.GetValue<int?>("RabbitMQ:Port") ?? 5672,

            UserName = _config["RabbitMQ:Username"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ username missing"),

            Password = _config["RabbitMQ:Password"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ password missing"),

            AutomaticRecoveryEnabled = true
        };

        await using var connection =
            await factory.CreateConnectionAsync(stoppingToken);

        var channels = new List<IChannel>();

        try
        {
            // Catalog: all three item events.
            await CreateConsumerAsync(
                connection,
                channels,
                "catalog.events",
                "recommendation.catalog",
                "catalog.item.*",
                stoppingToken);

            // UserService: only user creation.
            await CreateConsumerAsync(
                connection,
                channels,
                "user.events",
                "recommendation.user",
                "user.created",
                stoppingToken);

            // Loan: only loan creation.
            await CreateConsumerAsync(
                connection,
                channels,
                "loan.events",
                "recommendation.loan",
                "Loan.Created",
                stoppingToken);

            _logger.LogInformation(
                "Recommendation is listening to all three exchanges");

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            foreach (var channel in channels)
            {
                await channel.DisposeAsync();
            }
        }
    }

    // =========================================
    // CREATE CONSUMER + QUEUE + BINDING
    // =========================================

    private async Task CreateConsumerAsync(
        IConnection connection,
        List<IChannel> channels,
        string exchange,
        string queue,
        string routingKey,
        CancellationToken ct)
    {
        var channel = await connection.CreateChannelAsync(
            cancellationToken: ct);

        channels.Add(channel);

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        // Dead-letter exchange for invalid messages.
        await channel.ExchangeDeclareAsync(
            exchange: "recommendation.dead",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: "recommendation.dead",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: "recommendation.dead",
            exchange: "recommendation.dead",
            routingKey: "failed",
            cancellationToken: ct);

        // Declare durable consumer queue.
        await channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] =
                    "recommendation.dead",

                ["x-dead-letter-routing-key"] =
                    "failed"
            },
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queue,
            exchange: exchange,
            routingKey: routingKey,
            cancellationToken: ct);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: ct);

        // Configurable processing delay for autoscaling tests.
        // Defaults to zero during normal operation.
        var processingDelayMs = Math.Max(
            0,
            _config.GetValue<int>(
                "Recommendation:ProcessingDelayMs", 0));

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = System.Text.Encoding.UTF8
                    .GetString(args.Body.ToArray());

                // Simulate processing time during autoscaling tests.
                if (queue == "recommendation.catalog" &&
                    processingDelayMs > 0)
                {
                    await Task.Delay(processingDelayMs, ct);
                }

                await ProcessMessageAsync(
                    args.RoutingKey,
                    json);

                await channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false,
                    cancellationToken: ct);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Invalid event received: {RoutingKey}",
                    args.RoutingKey);

                // Reject invalid messages to the DLQ.
                await channel.BasicRejectAsync(
                    args.DeliveryTag,
                    requeue: false,
                    cancellationToken: ct);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                // Leave unacknowledged during shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Recommendation failed processing {RoutingKey}",
                    args.RoutingKey);

                // Development retry for transient failures.
                await Task.Delay(
                    TimeSpan.FromSeconds(2),
                    ct);

                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: ct);
            }
        };

        await channel.BasicConsumeAsync(
            queue: queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation(
            "Subscribed to {Exchange} ({RoutingKey}) on {Queue}",
            exchange,
            routingKey,
            queue);
    }

    // =========================================
    // PROCESS EVENTS BY ROUTING KEY
    // =========================================

    private async Task ProcessMessageAsync(
        string routingKey,
        string json)
    {
        switch (routingKey)
        {
            // -----------------------------
            // USER CREATED
            // -----------------------------
            case "user.created":
                {
                    var evt = Deserialize<UserCreatedEvent>(json);

                    if (evt.EventType != routingKey ||
                        evt.EventId == Guid.Empty ||
                        evt.SchemaVersion != 1 ||
                        evt.Data is null ||
                        evt.Data.UserId <= 0 ||
                        evt.Data.UserVersion <= 0)
                    {
                        throw new JsonException(
                            "Invalid user.created event");
                    }

                    await _repository.CreateUserAsync(evt);

                    _logger.LogInformation(
                        "Processed user.created for {UserId}",
                        evt.Data.UserId);

                    break;
                }

            // -----------------------------
            // CATALOG ITEM EVENTS
            // -----------------------------
            case "catalog.item.added":
            case "catalog.item.updated":
            case "catalog.item.deleted":
                {
                    var evt = Deserialize<CatalogItemEvent>(json);

                    if (evt.EventType != routingKey ||
                        evt.EventId == Guid.Empty ||
                        evt.SchemaVersion != 1 ||
                        evt.ItemId <= 0 ||
                        evt.ItemVersion <= 0 ||
                        evt.Item is null ||
                        evt.Item.Id != evt.ItemId ||
                        evt.Item.ItemVersion != evt.ItemVersion ||
                        string.IsNullOrWhiteSpace(evt.Item.Name))
                    {
                        throw new JsonException(
                            "Invalid Catalog item event");
                    }

                    bool shouldBeDeleted =
                        routingKey == "catalog.item.deleted";

                    if ((evt.Item.IsDeleted ?? false)
                        != shouldBeDeleted)
                    {
                        throw new JsonException(
                            "Catalog deletion state mismatch");
                    }

                    if (shouldBeDeleted &&
                        evt.Item.DeletedAtUtc is null)
                    {
                        throw new JsonException(
                            "Deletion timestamp missing");
                    }

                    await _repository.UpsertItemAsync(evt);

                    _logger.LogInformation(
                        "Processed {EventType} item {ItemId}, version {Version}",
                        routingKey,
                        evt.ItemId,
                        evt.ItemVersion);

                    break;
                }

            // -----------------------------
            // LOAN CREATED
            // -----------------------------
            case "Loan.Created":
                {
                    var evt = Deserialize<LoanCreatedEvent>(json);

                    if (evt.Id <= 0 ||
                        evt.LoanerId <= 0 ||
                        evt.ItemId <= 0)
                    {
                        throw new JsonException(
                            "Invalid Loan.Created event");
                    }

                    await _repository.CreateLoanAsync(evt);

                    _logger.LogInformation(
                        "Processed Loan.Created: {LoanId}",
                        evt.Id);

                    break;
                }

            default:
                throw new JsonException(
                    $"Unsupported routing key: {routingKey}");
        }
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new JsonException(
                $"Could not deserialize {typeof(T).Name}");
    }
}