using TrackLeadFlow.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Marketing.Application;
using TrackLeadFlow.Modules.Marketing.Infrastructure.Persistence;
using Xunit;

namespace TrackLeadFlow.Modules.Marketing.Application.Tests;

public sealed class RecordDeliveryTests
{
    private readonly NpgsqlConnectionFactory _factory = new();
    private readonly RecordDelivery _delivery = new();

    [Fact]
    public void A_click_stores_the_attribution_and_one_lead_submitted_delivery()
    {
        var tenantId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();
        try
        {
            WithSink(sink =>
            {
                _delivery.ContactStarted(sink, tenantId, conversationId, "click-1");
                _delivery.ContactStarted(sink, tenantId, conversationId, "click-1");
            });

            Assert.Equal("Pending", Status(tenantId, "LeadSubmitted", conversationId));
            Assert.True(Exists("""select exists(select 1 from marketing."Attributions" where "TenantId" = $1 and "CtwaClid" = 'click-1')""", tenantId));
            Assert.Equal(1, Count(tenantId, "LeadSubmitted", conversationId));
        }
        finally
        {
            Cleanup(tenantId);
        }
    }

    [Fact]
    public void A_missing_click_is_skipped_and_qualified_is_not_sent_twice()
    {
        var tenantId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();
        var leadId = Guid.CreateVersion7();
        try
        {
            WithSink(sink =>
            {
                _delivery.ContactStarted(sink, tenantId, conversationId, null);
                _delivery.Qualified(sink, tenantId, leadId, "click-1");
                _delivery.Qualified(sink, tenantId, leadId, "click-1");
            });

            Assert.Equal("Skipped", Status(tenantId, "LeadSubmitted", conversationId));
            Assert.Equal("Pending", Status(tenantId, "QualifiedLead", leadId));
            Assert.Equal(1, Count(tenantId, "QualifiedLead", leadId));
        }
        finally
        {
            Cleanup(tenantId);
        }
    }

    private void WithSink(Action<IDeliverySink> act)
    {
        using var session = _factory.Begin();
        act(new PostgresDeliverySink(new MarketingRepository(session.Transaction), DateTimeOffset.UtcNow));
        session.Commit();
    }

    private string Status(Guid tenantId, string businessEvent, Guid sourceId)
    {
        using var connection = _factory.Open();
        using var command = new Npgsql.NpgsqlCommand(
            """select "Status" from marketing."Deliveries" where "TenantId" = $1 and "BusinessEvent" = $2 and "SourceId" = $3""",
            connection);
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = tenantId });
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = businessEvent });
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = sourceId });
        return (string)command.ExecuteScalar()!;
    }

    private long Count(Guid tenantId, string businessEvent, Guid sourceId)
    {
        using var connection = _factory.Open();
        using var command = new Npgsql.NpgsqlCommand(
            """select count(*) from marketing."Deliveries" where "TenantId" = $1 and "BusinessEvent" = $2 and "SourceId" = $3""",
            connection);
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = tenantId });
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = businessEvent });
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = sourceId });
        return (long)command.ExecuteScalar()!;
    }

    private bool Exists(string sql, Guid tenantId)
    {
        using var connection = _factory.Open();
        return Db.Exists(connection, sql, tenantId);
    }

    private void Cleanup(Guid tenantId)
    {
        using var session = _factory.Begin();
        Db.Execute(session.Transaction, """delete from marketing."Deliveries" where "TenantId" = $1""", tenantId);
        Db.Execute(session.Transaction, """delete from marketing."Attributions" where "TenantId" = $1""", tenantId);
        session.Commit();
    }
}
