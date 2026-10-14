using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.Attendance.Infrastructure.Persistence;

public sealed class AttendanceRepository
{
    private readonly NpgsqlTransaction _transaction;

    public AttendanceRepository(NpgsqlTransaction transaction)
    {
        _transaction = transaction;
    }

    public void InsertWorkItem(Guid id, Guid tenantId, Guid leadId, Guid conversationId, string status, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into attendance."WorkItems"
                ("Id", "TenantId", "LeadId", "ConversationId", "Status", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, $6, $6)
            """,
            id,
            tenantId,
            leadId,
            conversationId,
            status,
            now);
    }

    public void InsertOutbox(Guid id, Guid tenantId, string type, string payload, DateTimeOffset occurredAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into attendance."OutboxMessages" ("Id", "TenantId", "Type", "Payload", "OccurredAt")
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
            insert into attendance."InboxMessages" ("Id", "EventId", "Type", "ProcessedAt")
            values ($1, $2, $3, $4)
            """,
            id,
            eventId,
            type,
            processedAt);
    }

    public static bool WorkItemExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from attendance."WorkItems" where "Id" = $1)""", id);

    public static bool OutboxExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from attendance."OutboxMessages" where "Id" = $1)""", id);
}
