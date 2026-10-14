using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.Marketing.Infrastructure.Persistence;

public sealed class MarketingRepository
{
    private readonly NpgsqlTransaction _transaction;

    public MarketingRepository(NpgsqlTransaction transaction)
    {
        _transaction = transaction;
    }

    public void InsertConnection(Guid id, Guid tenantId, string datasetId, string wabaId, string tokenSecretReference, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into marketing."Connections"
                ("Id", "TenantId", "DatasetId", "WabaId", "TokenSecretReference", "ContactEventName", "QualifiedEventName", "PurchaseEventName", "IsActive", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, 'LeadSubmitted', 'QualifiedLead', 'Purchase', true, $6, $6)
            """,
            id,
            tenantId,
            datasetId,
            wabaId,
            tokenSecretReference,
            now);
    }

    public void InsertAttribution(Guid id, Guid tenantId, Guid conversationId, string ctwaClid, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into marketing."Attributions" ("Id", "TenantId", "ConversationId", "CtwaClid", "CreatedAt")
            values ($1, $2, $3, $4, $5)
            """,
            id,
            tenantId,
            conversationId,
            ctwaClid,
            now);
    }

    public void InsertDelivery(Guid id, Guid tenantId, string businessEvent, Guid sourceId, string metaEventName, string status, string? skipReason, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into marketing."Deliveries"
                ("Id", "TenantId", "BusinessEvent", "SourceId", "MetaEventName", "Status", "SkipReason", "CreatedAt")
            values ($1, $2, $3, $4, $5, $6, $7, $8)
            """,
            id,
            tenantId,
            businessEvent,
            sourceId,
            metaEventName,
            status,
            skipReason,
            now);
    }

    public bool DeliveryExists(Guid tenantId, string businessEvent, Guid sourceId) =>
        Db.Exists(
            _transaction,
            """select exists(select 1 from marketing."Deliveries" where "TenantId" = $1 and "BusinessEvent" = $2 and "SourceId" = $3)""",
            tenantId,
            businessEvent,
            sourceId);

    public void InsertDelivery(Guid id, Guid tenantId, string businessEvent, Guid sourceId, string metaEventName, string status, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into marketing."Deliveries"
                ("Id", "TenantId", "BusinessEvent", "SourceId", "MetaEventName", "Status", "CreatedAt")
            values ($1, $2, $3, $4, $5, $6, $7)
            """,
            id,
            tenantId,
            businessEvent,
            sourceId,
            metaEventName,
            status,
            now);
    }

    public void InsertOutbox(Guid id, Guid tenantId, string type, string payload, DateTimeOffset occurredAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into marketing."OutboxMessages" ("Id", "TenantId", "Type", "Payload", "OccurredAt")
            values ($1, $2, $3, $4, $5)
            """,
            id,
            tenantId,
            type,
            payload,
            occurredAt);
    }

    public void InsertInbox(Guid id, Guid eventId, string type, DateTimeOffset processedAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into marketing."InboxMessages" ("Id", "EventId", "Type", "ProcessedAt")
            values ($1, $2, $3, $4)
            """,
            id,
            eventId,
            type,
            processedAt);
    }

    public static bool ConnectionExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from marketing."Connections" where "Id" = $1)""", id);

    public static bool OutboxExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from marketing."OutboxMessages" where "Id" = $1)""", id);
}
