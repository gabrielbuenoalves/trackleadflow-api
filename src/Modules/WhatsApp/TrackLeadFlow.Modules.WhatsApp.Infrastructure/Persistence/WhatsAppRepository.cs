using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.WhatsApp.Infrastructure.Persistence;

public sealed class WhatsAppRepository
{
    private readonly NpgsqlTransaction _transaction;

    public WhatsAppRepository(NpgsqlTransaction transaction)
    {
        _transaction = transaction;
    }

    public void InsertChannelAccount(Guid id, Guid tenantId, string phoneNumberId, string wabaId, string displayPhone, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into whatsapp."ChannelAccounts"
                ("Id", "TenantId", "PhoneNumberId", "WabaId", "DisplayPhone", "TokenSecretReference", "AppSecretReference", "IsActive", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, 'vault', 'vault', true, $6, $6)
            """,
            id,
            tenantId,
            phoneNumberId,
            wabaId,
            displayPhone,
            now);
    }

    public void InsertConversation(Guid id, Guid tenantId, string phone, string status, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into whatsapp."Conversations"
                ("Id", "TenantId", "Phone", "Status", "StartedAt", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, $5, $5)
            """,
            id,
            tenantId,
            phone,
            status,
            now);
    }

    public void InsertMessage(Guid id, Guid tenantId, Guid conversationId, string direction, string? body, string? externalMessageId, DateTimeOffset occurredAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into whatsapp."Messages"
                ("Id", "TenantId", "ConversationId", "Direction", "Body", "ExternalMessageId", "OccurredAt")
            values ($1, $2, $3, $4, $5, $6, $7)
            """,
            id,
            tenantId,
            conversationId,
            direction,
            body,
            externalMessageId,
            occurredAt);
    }

    public void InsertWebhookReceipt(Guid id, string provider, string providerEventId, DateTimeOffset receivedAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into whatsapp."WebhookReceipts" ("Id", "Provider", "ProviderEventId", "ReceivedAt")
            values ($1, $2, $3, $4)
            """,
            id,
            provider,
            providerEventId,
            receivedAt);
    }

    public bool ConversationExists(Guid id) =>
        Db.Exists(_transaction, """select exists(select 1 from whatsapp."Conversations" where "Id" = $1)""", id);

    public bool MessageExists(Guid id) =>
        Db.Exists(_transaction, """select exists(select 1 from whatsapp."Messages" where "Id" = $1)""", id);

    public (Guid TenantId, string PhoneNumberId)? FindActiveChannel(string phoneNumberId)
    {
        using var command = new NpgsqlCommand(
            """
            select "TenantId"
            from whatsapp."ChannelAccounts"
            where "PhoneNumberId" = $1 and "IsActive"
            """,
            _transaction.Connection,
            _transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = phoneNumberId });
        var tenantId = command.ExecuteScalar();
        return tenantId is Guid id ? (id, phoneNumberId) : null;
    }

    public bool ReceiptExists(string provider, string providerEventId) =>
        Db.Exists(
            _transaction,
            """select exists(select 1 from whatsapp."WebhookReceipts" where "Provider" = $1 and "ProviderEventId" = $2)""",
            provider,
            providerEventId);

    public Guid OpenConversation(Guid tenantId, string phone, DateTimeOffset now)
    {
        using var command = new NpgsqlCommand(
            """
            select "Id"
            from whatsapp."Conversations"
            where "TenantId" = $1 and "Phone" = $2 and "Status" = 'Open'
            """,
            _transaction.Connection,
            _transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter { Value = phone });
        var existing = command.ExecuteScalar();
        if (existing is Guid id)
        {
            return id;
        }

        var created = Guid.CreateVersion7();
        InsertConversation(created, tenantId, phone, "Open", now);
        return created;
    }

    public bool SetCtwaClidIfEmpty(Guid conversationId, string ctwaClid, DateTimeOffset now)
    {
        using var command = new NpgsqlCommand(
            """
            update whatsapp."Conversations"
            set "CtwaClid" = $2, "UpdatedAt" = $3
            where "Id" = $1 and "CtwaClid" is null
            """,
            _transaction.Connection,
            _transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = conversationId });
        command.Parameters.Add(new NpgsqlParameter { Value = ctwaClid });
        command.Parameters.Add(new NpgsqlParameter { Value = now });
        return command.ExecuteNonQuery() == 1;
    }

    public void InsertOutbox(Guid id, Guid tenantId, string type, string payload, DateTimeOffset occurredAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into whatsapp."OutboxMessages" ("Id", "TenantId", "Type", "Payload", "OccurredAt")
            values ($1, $2, $3, $4, $5)
            """,
            id,
            tenantId,
            type,
            payload,
            occurredAt);
    }
}
