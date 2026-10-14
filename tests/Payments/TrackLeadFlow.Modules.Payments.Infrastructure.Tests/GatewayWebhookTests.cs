using System.Security.Cryptography;
using System.Text;
using TrackLeadFlow.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Marketing.Application;
using TrackLeadFlow.Modules.Marketing.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Payments.Infrastructure.Http;
using TrackLeadFlow.Modules.Payments.Infrastructure.Persistence;
using Xunit;

namespace TrackLeadFlow.Modules.Payments.Infrastructure.Tests;

public sealed class GatewayWebhookTests
{
    private const string Secret = "gateway-secret";
    private readonly NpgsqlConnectionFactory _factory = new();

    [Fact]
    public void An_invalid_signature_does_not_mark_the_charge_paid()
    {
        var chargeId = InsertCharge();
        try
        {
            var result = Endpoint().Receive(Body(chargeId, "evt-1", "click-1"), "nope");
            Assert.Equal(401, result.StatusCode);
            Assert.Equal("Pending", Status(chargeId));
        }
        finally
        {
            Cleanup(chargeId);
        }
    }

    [Fact]
    public void A_valid_notice_marks_the_charge_paid_once_and_records_purchase()
    {
        var chargeId = InsertCharge();
        var tenantId = TenantOf(chargeId);
        try
        {
            var body = Body(chargeId, "evt-2", "click-9");
            var signature = Sign(body);
            Assert.Equal(200, Endpoint().Receive(body, signature).StatusCode);
            Assert.Equal(200, Endpoint().Receive(body, signature).StatusCode);
            Assert.Equal("Paid", Status(chargeId));
            Assert.Equal(1, CountDeliveries(tenantId, chargeId));
        }
        finally
        {
            Cleanup(chargeId);
        }
    }

    [Fact]
    public void A_pix_receipt_in_the_chat_does_not_change_payment_status()
    {
        var chargeId = InsertCharge();
        try
        {
            Assert.False(GatewayWebhookEndpoint.WhatsAppBodyConfirmsPayment("segue o comprovante do pix"));
            Assert.Equal("Pending", Status(chargeId));
        }
        finally
        {
            Cleanup(chargeId);
        }
    }

    private GatewayWebhookEndpoint Endpoint() =>
        new(_factory, (transaction, paid) =>
        {
            new RecordDelivery().Purchase(
                new PostgresDeliverySink(new MarketingRepository(transaction), DateTimeOffset.UtcNow),
                paid.TenantId,
                paid.ChargeId,
                paid.CtwaClid,
                paid.Amount,
                paid.Currency);
        }, Secret);

    private Guid InsertCharge()
    {
        var id = Guid.CreateVersion7();
        using var session = _factory.Begin();
        new PaymentsRepository(session.Transaction).InsertCharge(id, Guid.CreateVersion7(), Guid.CreateVersion7(), "Análise", 150m, "BRL", "Pending", "gateway", DateTimeOffset.UtcNow);
        session.Commit();
        return id;
    }

    private string Status(Guid chargeId)
    {
        using var connection = _factory.Open();
        using var command = new Npgsql.NpgsqlCommand("""select "Status" from payments."Charges" where "Id" = $1""", connection);
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = chargeId });
        return (string)command.ExecuteScalar()!;
    }

    private Guid TenantOf(Guid chargeId)
    {
        using var connection = _factory.Open();
        using var command = new Npgsql.NpgsqlCommand("""select "TenantId" from payments."Charges" where "Id" = $1""", connection);
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = chargeId });
        return (Guid)command.ExecuteScalar()!;
    }

    private long CountDeliveries(Guid tenantId, Guid chargeId)
    {
        using var connection = _factory.Open();
        using var command = new Npgsql.NpgsqlCommand(
            """select count(*) from marketing."Deliveries" where "TenantId" = $1 and "BusinessEvent" = 'Purchase' and "SourceId" = $2""",
            connection);
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = tenantId });
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = chargeId });
        return (long)command.ExecuteScalar()!;
    }

    private void Cleanup(Guid chargeId)
    {
        using var session = _factory.Begin();
        Db.Execute(session.Transaction, """delete from marketing."Deliveries" where "SourceId" = $1""", chargeId);
        Db.Execute(session.Transaction, """delete from payments."WebhookReceipts" where "Provider" = 'gateway' and "ProviderEventId" in ('evt-1', 'evt-2')""");
        Db.Execute(session.Transaction, """delete from payments."Charges" where "Id" = $1""", chargeId);
        session.Commit();
    }

    private static string Body(Guid chargeId, string eventId, string ctwaClid) =>
        "{\"ChargeId\":\"" + chargeId + "\",\"EventId\":\"" + eventId + "\",\"CtwaClid\":\"" + ctwaClid + "\"}";

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
}
