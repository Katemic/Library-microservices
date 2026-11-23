using System.Text.Json;
using FluentAssertions;
using Recommendation.Consumers;
using Recommendation.Contracts;

namespace Recommendation.UnitTest;

[TestClass]
public sealed class EventValidatorTests
{
    [TestMethod]
    public void ValidateUserCreated_ValidEvent_ShouldPass()
    {
        var evt = CreateUserEvent();

        var result = EventValidator.ValidateUserCreated(
            evt, "user.created");

        result.Should().BeSameAs(evt);
    }

    [TestMethod]
    public void ValidateUserCreated_EmptyEventId_ShouldThrow()
    {
        var evt = CreateUserEvent();
        evt.EventId = Guid.Empty;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateUserCreated(
                evt, "user.created"));
    }

    [TestMethod]
    public void ValidateUserCreated_InvalidRoutingKey_ShouldThrow()
    {
        var evt = CreateUserEvent();

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateUserCreated(
                evt, "wrong.routing.key"));
    }

    [TestMethod]
    public void ValidateUserCreated_InvalidSchemaVersion_ShouldThrow()
    {
        var evt = CreateUserEvent();
        evt.SchemaVersion = 2;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateUserCreated(
                evt, "user.created"));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void ValidateUserCreated_InvalidUserId_ShouldThrow(int userId)
    {
        var evt = CreateUserEvent();
        evt.Data.UserId = userId;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateUserCreated(
                evt, "user.created"));
    }

    [TestMethod]
    public void ValidateCatalogItem_ValidAddedEvent_ShouldPass()
    {
        var evt = CreateCatalogEvent();

        var result = EventValidator.ValidateCatalogItem(
            evt, "catalog.item.added");

        result.Should().BeSameAs(evt);
    }

    [TestMethod]
    public void ValidateCatalogItem_MismatchedItemId_ShouldThrow()
    {
        var evt = CreateCatalogEvent();
        evt.Item.Id = 999;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateCatalogItem(
                evt, "catalog.item.added"));
    }

    [TestMethod]
    public void ValidateCatalogItem_MismatchedVersion_ShouldThrow()
    {
        var evt = CreateCatalogEvent();
        evt.Item.ItemVersion = 2;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateCatalogItem(
                evt, "catalog.item.added"));
    }

    [TestMethod]
    public void ValidateCatalogItem_EmptyName_ShouldThrow()
    {
        var evt = CreateCatalogEvent();
        evt.Item.Name = " ";

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateCatalogItem(
                evt, "catalog.item.added"));
    }

    [TestMethod]
    public void ValidateCatalogItem_DeletedWithoutTimestamp_ShouldThrow()
    {
        var evt = CreateCatalogEvent("catalog.item.deleted");
        evt.Item.IsDeleted = true;
        evt.Item.DeletedAtUtc = null;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateCatalogItem(
                evt, "catalog.item.deleted"));
    }

    [TestMethod]
    public void ValidateCatalogItem_DeletedWithTimestamp_ShouldPass()
    {
        var evt = CreateCatalogEvent("catalog.item.deleted");
        evt.Item.IsDeleted = true;
        evt.Item.DeletedAtUtc = DateTime.UtcNow;

        var result = EventValidator.ValidateCatalogItem(
            evt, "catalog.item.deleted");

        result.Should().BeSameAs(evt);
    }

    [TestMethod]
    public void ValidateCatalogItem_AddedButMarkedDeleted_ShouldThrow()
    {
        var evt = CreateCatalogEvent();
        evt.Item.IsDeleted = true;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateCatalogItem(
                evt, "catalog.item.added"));
    }

    [TestMethod]
    public void ValidateCatalogItem_UpdatedButDeleted_ShouldThrow()
    {
        var evt = CreateCatalogEvent("catalog.item.updated");
        evt.Item.IsDeleted = true;

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateCatalogItem(
                evt, "catalog.item.updated"));
    }

    [TestMethod]
    public void ValidateLoanCreated_ValidEvent_ShouldPass()
    {
        var evt = new LoanCreatedEvent
        {
            Id = 1,
            LoanerId = 10,
            ItemId = 20
        };

        var result = EventValidator.ValidateLoanCreated(evt);

        result.Should().BeSameAs(evt);
    }

    [TestMethod]
    [DataRow(0, 10, 20)]
    [DataRow(-1, 10, 20)]
    [DataRow(1, 0, 20)]
    [DataRow(1, -1, 20)]
    [DataRow(1, 10, 0)]
    [DataRow(1, 10, -1)]
    public void ValidateLoanCreated_InvalidIds_ShouldThrow(
        int loanId,
        int loanerId,
        int itemId)
    {
        var evt = new LoanCreatedEvent
        {
            Id = loanId,
            LoanerId = loanerId,
            ItemId = itemId
        };

        Assert.ThrowsExactly<JsonException>(() =>
            EventValidator.ValidateLoanCreated(evt));
    }

    private static UserCreatedEvent CreateUserEvent()
    {
        return new UserCreatedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "user.created",
            SchemaVersion = 1,
            Data = new UserEventData
            {
                UserId = 10,
                UserVersion = 1
            }
        };
    }

    private static CatalogItemEvent CreateCatalogEvent(
        string routingKey = "catalog.item.added")
    {
        return new CatalogItemEvent
        {
            EventId = Guid.NewGuid(),
            EventType = routingKey,
            SchemaVersion = 1,
            ItemId = 20,
            ItemVersion = 1,
            Item = new ItemSnapshot
            {
                Id = 20,
                Name = "The Hobbit",
                MediaType = "Book",
                ItemVersion = 1,
                IsDeleted = false
            }
        };
    }
}
