using System.Security.Cryptography;
using System.Text;
using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Identity.Infrastructure.Persistence;
using TrackLeadFlow.Modules.WhatsApp.Infrastructure.External.CloudApi;
using TrackLeadFlow.Modules.WhatsApp.Infrastructure.Http;
using TrackLeadFlow.Modules.WhatsApp.Infrastructure.Persistence;
using Xunit;

namespace TrackLeadFlow.Modules.WhatsApp.Infrastructure.Tests;

public sealed class CloudApiWebhookTests
{
    private const string AppSecret = "app-secret";
    private const string VerifyToken = "verify-token";
    private readonly NpgsqlConnectionFactory _factory = new();

    [Fact]
    public void Get_returns_the_challenge_when_the_verify_token_matches()
    {
        var endpoint = Endpoint();
        var result = endpoint.Verify("subscribe", VerifyToken, "challenge-1");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("challenge-1", result.Body);
    }

    [Fact]
    public void Post_without_a_valid_signature_does_not_store_a_receipt()
    {
        var tenantId = Guid.CreateVersion7();
        var phoneNumberId = NewToken();
        var messageId = NewToken();
        PrepareChannel(tenantId, phoneNumberId);
        try
        {
            var body = Payload(phoneNumberId, "5511999990001", messageId, "oi", "click-1");
            var result = Endpoint().Receive(body, "sha256=0000");

            Assert.Equal(401, result.StatusCode);
            Assert.False(Exists("""select exists(select 1 from whatsapp."WebhookReceipts" where "ProviderEventId" = $1)""", messageId));
        }
        finally
        {
            Cleanup(tenantId, phoneNumberId);
        }
    }

    [Fact]
    public void The_same_provider_event_does_not_create_another_message()
    {
        var tenantId = Guid.CreateVersion7();
        var phoneNumberId = NewToken();
        var messageId = NewToken();
        PrepareChannel(tenantId, phoneNumberId);
        try
        {
            var body = Payload(phoneNumberId, "5511999990002", messageId, "oi", null);
            var endpoint = Endpoint();
            var signature = Sign(body);
            Assert.Equal(200, endpoint.Receive(body, signature).StatusCode);
            Assert.Equal(200, endpoint.Receive(body, signature).StatusCode);

            Assert.Equal(1, Count("""select count(*) from whatsapp."Messages" where "ExternalMessageId" = $1""", messageId));
        }
        finally
        {
            Cleanup(tenantId, phoneNumberId);
        }
    }

    [Fact]
    public void A_registered_specialist_phone_publishes_an_operator_message()
    {
        var tenantId = Guid.CreateVersion7();
        var phoneNumberId = NewToken();
        var messageId = NewToken();
        PrepareChannel(tenantId, phoneNumberId);
        PrepareUser(tenantId, "5511999990003");
        try
        {
            var body = Payload(phoneNumberId, "+55 (11) 99999-0003", messageId, "manda a proposta", "click-ignored");
            var result = Endpoint().Receive(body, Sign(body));

            Assert.Equal(200, result.StatusCode);
            Assert.True(OutboxExists(tenantId, "OperatorMessageReceived"));
            Assert.False(OutboxExists(tenantId, "InboundMessageReceived"));
            Assert.False(OutboxExists(tenantId, "ContactStarted"));
        }
        finally
        {
            Cleanup(tenantId, phoneNumberId);
        }
    }

    [Fact]
    public void An_unknown_phone_stores_the_message_and_the_click_once()
    {
        var tenantId = Guid.CreateVersion7();
        var phoneNumberId = NewToken();
        var firstId = NewToken();
        var secondId = NewToken();
        PrepareChannel(tenantId, phoneNumberId);
        try
        {
            var endpoint = Endpoint();
            var first = Payload(phoneNumberId, "5511999990004", firstId, "oi", "click-1");
            var second = Payload(phoneNumberId, "5511999990004", secondId, "de novo", "click-2");
            Assert.Equal(200, endpoint.Receive(first, Sign(first)).StatusCode);
            Assert.Equal(200, endpoint.Receive(second, Sign(second)).StatusCode);

            Assert.True(OutboxExists(tenantId, "InboundMessageReceived"));
            Assert.Equal(1, Count("""select count(*) from whatsapp."OutboxMessages" where "TenantId" = $1 and "Type" = 'ContactStarted'""", tenantId));
            Assert.True(Exists("""select exists(select 1 from whatsapp."Conversations" where "TenantId" = $1 and "CtwaClid" = 'click-1')""", tenantId));
            Assert.Equal(2, Count("""select count(*) from whatsapp."Messages" where "TenantId" = $1""", tenantId));
        }
        finally
        {
            Cleanup(tenantId, phoneNumberId);
        }
    }

    private CloudApiWebhookEndpoint Endpoint() =>
        new(_factory, new OperatorPhoneLookup(_factory), new FixedSecret(AppSecret), VerifyToken);

    private void PrepareChannel(Guid tenantId, string phoneNumberId)
    {
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        new IdentityRepository(session.Transaction).InsertTenant(tenantId, "Tenant", now);
        new WhatsAppRepository(session.Transaction).InsertChannelAccount(Guid.CreateVersion7(), tenantId, phoneNumberId, "waba-1", "5511000000000", now);
        session.Commit();
    }

    private void PrepareUser(Guid tenantId, string phone)
    {
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var users = new IdentityRepository(session.Transaction);
        users.InsertUser(Guid.CreateVersion7(), tenantId, phone + "@example.com", "Especialista", phone, "hash", now);
        session.Commit();
    }

    private void Cleanup(Guid tenantId, string phoneNumberId)
    {
        using var session = _factory.Begin();
        var tx = session.Transaction;
        Db.Execute(tx, """delete from whatsapp."WebhookReceipts" where "ProviderEventId" in (select "ExternalMessageId" from whatsapp."Messages" where "TenantId" = $1)""", tenantId);
        Db.Execute(tx, """delete from whatsapp."Messages" where "TenantId" = $1""", tenantId);
        Db.Execute(tx, """delete from whatsapp."OutboxMessages" where "TenantId" = $1""", tenantId);
        Db.Execute(tx, """delete from whatsapp."Conversations" where "TenantId" = $1""", tenantId);
        Db.Execute(tx, """delete from whatsapp."ChannelAccounts" where "PhoneNumberId" = $1""", phoneNumberId);
        Db.Execute(tx, """delete from identity."UserRoles" where "UserId" in (select "Id" from identity."Users" where "TenantId" = $1)""", tenantId);
        Db.Execute(tx, """delete from identity."Users" where "TenantId" = $1""", tenantId);
        Db.Execute(tx, """delete from identity."Tenants" where "Id" = $1""", tenantId);
        session.Commit();
    }

    private bool Exists(string sql, object arg)
    {
        using var connection = _factory.Open();
        return Db.Exists(connection, sql, arg);
    }

    private bool OutboxExists(Guid tenantId, string type)
    {
        using var connection = _factory.Open();
        return Db.Exists(connection, """select exists(select 1 from whatsapp."OutboxMessages" where "TenantId" = $1 and "Type" = $2)""", tenantId, type);
    }

    private long Count(string sql, object arg)
    {
        using var connection = _factory.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = arg });
        return (long)command.ExecuteScalar()!;
    }

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Payload(string phoneNumberId, string from, string messageId, string text, string? ctwaClid)
    {
        var referral = ctwaClid is null ? "" : ",\"referral\":{\"ctwa_clid\":\"" + ctwaClid + "\"}";
        return "{\"entry\":[{\"changes\":[{\"value\":{\"metadata\":{\"phone_number_id\":\"" + phoneNumberId
            + "\"},\"messages\":[{\"from\":\"" + from + "\",\"id\":\"" + messageId
            + "\",\"timestamp\":\"1710000000\",\"type\":\"text\",\"text\":{\"body\":\"" + text + "\"}" + referral + "}]}}]}]}";
    }

    private static string NewToken() => Guid.CreateVersion7().ToString("N");

    private sealed class FixedSecret : IChannelAppSecret
    {
        private readonly string _secret;

        public FixedSecret(string secret) => _secret = secret;

        public string? Find(string phoneNumberId) => _secret;
    }
}
