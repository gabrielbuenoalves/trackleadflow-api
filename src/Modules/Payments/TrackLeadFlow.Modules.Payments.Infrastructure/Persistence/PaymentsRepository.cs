using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.Payments.Infrastructure.Persistence;

public sealed class PaymentsRepository
{
    private readonly NpgsqlTransaction _transaction;

    public PaymentsRepository(NpgsqlTransaction transaction)
    {
        _transaction = transaction;
    }

    public void InsertCharge(Guid id, Guid tenantId, Guid leadId, string label, decimal amount, string currency, string status, string provider, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into payments."Charges"
                ("Id", "TenantId", "LeadId", "Label", "Amount", "Currency", "Status", "Provider", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $9)
            """,
            id,
            tenantId,
            leadId,
            label,
            amount,
            currency,
            status,
            provider,
            now);
    }

    public void InsertOutbox(Guid id, Guid tenantId, string type, string payload, DateTimeOffset occurredAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into payments."OutboxMessages" ("Id", "TenantId", "Type", "Payload", "OccurredAt")
            values ($1, $2, $3, $4, $5)
            """,
            id,
            tenantId,
            type,
            payload,
            occurredAt);
    }

    public void InsertWebhookReceipt(Guid id, string provider, string providerEventId, DateTimeOffset receivedAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into payments."WebhookReceipts" ("Id", "Provider", "ProviderEventId", "ReceivedAt")
            values ($1, $2, $3, $4)
            """,
            id,
            provider,
            providerEventId,
            receivedAt);
    }

    public bool ReceiptExists(string provider, string providerEventId) =>
        Db.Exists(
            _transaction,
            """select exists(select 1 from payments."WebhookReceipts" where "Provider" = $1 and "ProviderEventId" = $2)""",
            provider,
            providerEventId);

    public ChargeRow? FindCharge(Guid id)
    {
        using var command = new NpgsqlCommand(
            """
            select "Id", "TenantId", "Amount", "Currency", "Status"
            from payments."Charges"
            where "Id" = $1
            """,
            _transaction.Connection,
            _transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ChargeRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3).Trim(), reader.GetString(4));
    }

    public void MarkPaid(Guid id, DateTimeOffset paidAt)
    {
        Db.Execute(
            _transaction,
            """
            update payments."Charges"
            set "Status" = 'Paid', "PaidAt" = $2, "UpdatedAt" = $2, "RowVersion" = "RowVersion" + 1
            where "Id" = $1 and "Status" = 'Pending'
            """,
            id,
            paidAt);
    }

    public void SaveCheckout(Guid id, Guid tenantId, Guid leadId, string label, decimal amount, string currency, string externalChargeId, string checkoutReference, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into payments."Charges"
                ("Id", "TenantId", "LeadId", "Label", "Amount", "Currency", "Status", "Provider", "ExternalChargeId", "CheckoutReference", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, $6, 'Pending', 'gateway', $7, $8, $9, $9)
            """,
            id,
            tenantId,
            leadId,
            label,
            amount,
            currency,
            externalChargeId,
            checkoutReference,
            now);
    }

    public static bool ChargeExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from payments."Charges" where "Id" = $1)""", id);

    public static bool OutboxExists(NpgsqlConnection connection, Guid id) =>
        Db.Exists(connection, """select exists(select 1 from payments."OutboxMessages" where "Id" = $1)""", id);
}
