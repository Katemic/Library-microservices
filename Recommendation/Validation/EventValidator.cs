using System.Text.Json;
using Recommendation.Contracts;

namespace Recommendation.Consumers;

public static class EventValidator
{
    public static UserCreatedEvent ValidateUserCreated(
        UserCreatedEvent evt,
        string routingKey)
    {
        if (evt.EventType != routingKey ||
            evt.EventId == Guid.Empty ||
            evt.SchemaVersion != 1 ||
            evt.Data is null ||
            evt.Data.UserId <= 0 ||
            evt.Data.UserVersion <= 0)
        {
            throw new JsonException("Invalid user.created event");
        }

        return evt;
    }

    public static CatalogItemEvent ValidateCatalogItem(
        CatalogItemEvent evt,
        string routingKey)
    {
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
            throw new JsonException("Invalid Catalog item event");
        }

        bool shouldBeDeleted =
            routingKey == "catalog.item.deleted";

        if ((evt.Item.IsDeleted ?? false) != shouldBeDeleted)
        {
            throw new JsonException("Catalog deletion state mismatch");
        }

        if (shouldBeDeleted && evt.Item.DeletedAtUtc is null)
        {
            throw new JsonException("Deletion timestamp missing");
        }

        return evt;
    }

    public static LoanCreatedEvent ValidateLoanCreated(
        LoanCreatedEvent evt)
    {
        if (evt.Id <= 0 ||
            evt.LoanerId <= 0 ||
            evt.ItemId <= 0)
        {
            throw new JsonException("Invalid Loan.Created event");
        }

        return evt;
    }
}
