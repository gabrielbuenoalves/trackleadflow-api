using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.FollowUp.Infrastructure.Persistence;

public sealed class FollowUpRepository
{
    private readonly NpgsqlTransaction _transaction;

    public FollowUpRepository(NpgsqlTransaction transaction)
    {
        _transaction = transaction;
    }

    public void InsertSequence(Guid id, Guid tenantId, string name, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into followup."Sequences" ("Id", "TenantId", "Name", "IsActive", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, true, $4, $4)
            """,
            id,
            tenantId,
            name,
            now);
    }

    public void InsertOutbox(Guid id, Guid tenantId, string type, string payload, DateTimeOffset occurredAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into followup."OutboxMessages" ("Id", "TenantId", "Type", "Payload", "OccurredAt")
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
            insert into followup."InboxMessages" ("Id", "EventId", "Type", "ProcessedAt")
            values ($1, $2, $3, $4)
            """,
            id,
            eventId,
            type,
            processedAt);
    }

    public static bool SequenceExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from followup."Sequences" where "Id" = $1)""", id);

    public static bool OutboxExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from followup."OutboxMessages" where "Id" = $1)""", id);
}
