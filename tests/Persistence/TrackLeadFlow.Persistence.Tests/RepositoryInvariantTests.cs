using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Attendance.Infrastructure.Persistence;
using TrackLeadFlow.Modules.FollowUp.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Identity.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Marketing.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Payments.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Qualification.Infrastructure.Persistence;
using TrackLeadFlow.Modules.WhatsApp.Infrastructure.Persistence;
using Xunit;

namespace TrackLeadFlow.Persistence.Tests;

public sealed class RepositoryInvariantTests
{
    private readonly NpgsqlConnectionFactory _factory = new();

    [Fact]
    public void Identity_saves_tenant_user_and_role()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new IdentityRepository(session.Transaction);
        repo.InsertTenant(tenantId, "Tenant", now);
        repo.InsertUser(userId, tenantId, "ada@example.com", "Ada", "5511999990000", "hash", now);
        repo.InsertRole(userId, "Specialist");

        Assert.True(repo.UserExists(userId));
        Assert.True(repo.RoleExists(userId, "Specialist"));
    }

    [Fact]
    public void Identity_rejects_duplicate_email_in_the_same_tenant()
    {
        var tenantId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new IdentityRepository(session.Transaction);
        repo.InsertTenant(tenantId, "Tenant", now);
        repo.InsertUser(Guid.CreateVersion7(), tenantId, "ada@example.com", "Ada", null, "hash", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertUser(Guid.CreateVersion7(), tenantId, "ada@example.com", "Ada 2", null, "hash", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void Identity_rejects_duplicate_phone_and_allows_users_without_one()
    {
        var tenantId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new IdentityRepository(session.Transaction);
        repo.InsertTenant(tenantId, "Tenant", now);
        repo.InsertUser(Guid.CreateVersion7(), tenantId, "a@example.com", "A", null, "hash", now);
        repo.InsertUser(Guid.CreateVersion7(), tenantId, "b@example.com", "B", null, "hash", now);
        repo.InsertUser(Guid.CreateVersion7(), tenantId, "c@example.com", "C", "5511888880000", "hash", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertUser(Guid.CreateVersion7(), tenantId, "d@example.com", "D", "5511888880000", "hash", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void WhatsApp_saves_account_conversation_and_message()
    {
        var tenantId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new WhatsAppRepository(session.Transaction);
        repo.InsertChannelAccount(Guid.CreateVersion7(), tenantId, "phone-1", "waba-1", "5511000000000", now);
        repo.InsertConversation(conversationId, tenantId, "5511999990000", "Open", now);
        repo.InsertMessage(messageId, tenantId, conversationId, "Inbound", "oi", "wamid.1", now);

        Assert.True(repo.ConversationExists(conversationId));
        Assert.True(repo.MessageExists(messageId));
    }

    [Fact]
    public void WhatsApp_rejects_a_second_open_conversation_for_the_same_phone()
    {
        var tenantId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new WhatsAppRepository(session.Transaction);
        repo.InsertConversation(Guid.CreateVersion7(), tenantId, "5511999990000", "Open", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertConversation(Guid.CreateVersion7(), tenantId, "5511999990000", "Open", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void WhatsApp_rejects_a_duplicate_external_message_id_and_allows_a_missing_one()
    {
        var tenantId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new WhatsAppRepository(session.Transaction);
        repo.InsertConversation(conversationId, tenantId, "5511999990000", "Open", now);
        repo.InsertMessage(Guid.CreateVersion7(), tenantId, conversationId, "Inbound", "a", null, now);
        repo.InsertMessage(Guid.CreateVersion7(), tenantId, conversationId, "Inbound", "b", null, now);
        repo.InsertMessage(Guid.CreateVersion7(), tenantId, conversationId, "Inbound", "c", "wamid.1", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertMessage(Guid.CreateVersion7(), tenantId, conversationId, "Inbound", "d", "wamid.1", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void WhatsApp_rejects_a_duplicate_webhook_receipt()
    {
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new WhatsAppRepository(session.Transaction);
        repo.InsertWebhookReceipt(Guid.CreateVersion7(), "cloud-api", "evt-1", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertWebhookReceipt(Guid.CreateVersion7(), "cloud-api", "evt-1", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void Qualification_saves_script_lead_and_answer()
    {
        var tenantId = Guid.CreateVersion7();
        var scriptId = Guid.CreateVersion7();
        var stepId = Guid.CreateVersion7();
        var leadId = Guid.CreateVersion7();
        var answerId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new QualificationRepository(session.Transaction);
        repo.InsertCriteriaSet(scriptId, tenantId, "Roteiro", true, now);
        repo.InsertStep(stepId, scriptId, 1, "Question", "Qual é o caso?");
        repo.InsertLead(leadId, tenantId, Guid.CreateVersion7(), scriptId, "InTriage", now);
        repo.InsertAnswer(answerId, leadId, stepId, "roubo", now);

        Assert.True(repo.LeadExists(leadId));
        Assert.True(repo.AnswerExists(answerId));
    }

    [Fact]
    public void Qualification_rejects_a_second_lead_for_the_same_conversation()
    {
        var tenantId = Guid.CreateVersion7();
        var scriptId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new QualificationRepository(session.Transaction);
        repo.InsertCriteriaSet(scriptId, tenantId, "Roteiro", true, now);
        repo.InsertLead(Guid.CreateVersion7(), tenantId, conversationId, scriptId, "InTriage", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertLead(Guid.CreateVersion7(), tenantId, conversationId, scriptId, "InTriage", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void Qualification_rejects_a_second_active_script()
    {
        var tenantId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new QualificationRepository(session.Transaction);
        repo.InsertCriteriaSet(Guid.CreateVersion7(), tenantId, "A", true, now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertCriteriaSet(Guid.CreateVersion7(), tenantId, "B", true, now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void FollowUp_commits_sequence_and_outbox_together_and_rolls_both_back()
    {
        var tenantId = Guid.CreateVersion7();
        var sequenceId = Guid.CreateVersion7();
        var outboxId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        using (var session = _factory.Begin())
        {
            var repo = new FollowUpRepository(session.Transaction);
            repo.InsertSequence(sequenceId, tenantId, "Silêncio", now);
            repo.InsertOutbox(outboxId, tenantId, "ReplyExpected", "{}", now);
            Assert.Throws<PostgresException>(() => repo.InsertOutbox(outboxId, tenantId, "ReplyExpected", "{}", now));
        }

        using var check = _factory.Open();
        Assert.False(FollowUpRepository.SequenceExists(check, sequenceId));
        Assert.False(FollowUpRepository.OutboxExists(check, outboxId));
    }

    [Fact]
    public void FollowUp_makes_sequence_and_outbox_visible_in_the_same_commit()
    {
        var tenantId = Guid.CreateVersion7();
        var sequenceId = Guid.CreateVersion7();
        var outboxId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        try
        {
            using (var session = _factory.Begin())
            {
                var repo = new FollowUpRepository(session.Transaction);
                repo.InsertSequence(sequenceId, tenantId, "Silêncio", now);
                repo.InsertOutbox(outboxId, tenantId, "ReplyExpected", "{}", now);
                session.Commit();
            }

            using var check = _factory.Open();
            Assert.True(FollowUpRepository.SequenceExists(check, sequenceId));
            Assert.True(FollowUpRepository.OutboxExists(check, outboxId));
        }
        finally
        {
            DeleteCommitted(
                """delete from followup."OutboxMessages" where "Id" = $1""",
                """delete from followup."Sequences" where "Id" = $1""",
                outboxId,
                sequenceId);
        }
    }

    [Fact]
    public void FollowUp_rejects_a_duplicate_inbox_message()
    {
        var eventId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new FollowUpRepository(session.Transaction);
        repo.InsertInbox(Guid.CreateVersion7(), eventId, "InboundMessageReceived", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertInbox(Guid.CreateVersion7(), eventId, "InboundMessageReceived", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void Attendance_makes_work_item_and_outbox_visible_in_the_same_commit()
    {
        var workItemId = Guid.CreateVersion7();
        var outboxId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        try
        {
            using (var session = _factory.Begin())
            {
                var repo = new AttendanceRepository(session.Transaction);
                repo.InsertWorkItem(workItemId, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Queued", now);
                repo.InsertOutbox(outboxId, Guid.CreateVersion7(), "LeadQualified", "{}", now);
                session.Commit();
            }

            using var check = _factory.Open();
            Assert.True(AttendanceRepository.WorkItemExists(check, workItemId));
            Assert.True(AttendanceRepository.OutboxExists(check, outboxId));
        }
        finally
        {
            DeleteCommitted(
                """delete from attendance."OutboxMessages" where "Id" = $1""",
                """delete from attendance."WorkItems" where "Id" = $1""",
                outboxId,
                workItemId);
        }
    }

    [Fact]
    public void Attendance_rejects_a_duplicate_inbox_message()
    {
        var eventId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new AttendanceRepository(session.Transaction);
        repo.InsertInbox(Guid.CreateVersion7(), eventId, "LeadQualified", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertInbox(Guid.CreateVersion7(), eventId, "LeadQualified", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void Payments_makes_charge_and_outbox_visible_in_the_same_commit()
    {
        var chargeId = Guid.CreateVersion7();
        var outboxId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        try
        {
            using (var session = _factory.Begin())
            {
                var repo = new PaymentsRepository(session.Transaction);
                repo.InsertCharge(chargeId, tenantId, Guid.CreateVersion7(), "Análise", 150m, "BRL", "Pending", "gateway", now);
                repo.InsertOutbox(outboxId, tenantId, "ChargeCreated", "{}", now);
                session.Commit();
            }

            using var check = _factory.Open();
            Assert.True(PaymentsRepository.ChargeExists(check, chargeId));
            Assert.True(PaymentsRepository.OutboxExists(check, outboxId));
        }
        finally
        {
            DeleteCommitted(
                """delete from payments."OutboxMessages" where "Id" = $1""",
                """delete from payments."Charges" where "Id" = $1""",
                outboxId,
                chargeId);
        }
    }

    [Fact]
    public void Payments_rejects_a_duplicate_webhook_receipt()
    {
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new PaymentsRepository(session.Transaction);
        repo.InsertWebhookReceipt(Guid.CreateVersion7(), "gateway", "evt-1", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertWebhookReceipt(Guid.CreateVersion7(), "gateway", "evt-1", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public void Marketing_makes_connection_and_outbox_visible_in_the_same_commit()
    {
        var connectionId = Guid.CreateVersion7();
        var outboxId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        try
        {
            using (var session = _factory.Begin())
            {
                var repo = new MarketingRepository(session.Transaction);
                repo.InsertConnection(connectionId, tenantId, "dataset-1", "waba-1", "secret-ref", now);
                repo.InsertOutbox(outboxId, tenantId, "ContactStarted", "{}", now);
                session.Commit();
            }

            using var check = _factory.Open();
            Assert.True(MarketingRepository.ConnectionExists(check, connectionId));
            Assert.True(MarketingRepository.OutboxExists(check, outboxId));
        }
        finally
        {
            DeleteCommitted(
                """delete from marketing."OutboxMessages" where "Id" = $1""",
                """delete from marketing."Connections" where "Id" = $1""",
                outboxId,
                connectionId);
        }
    }

    [Fact]
    public void Marketing_rejects_a_duplicate_delivery_and_inbox_message()
    {
        var tenantId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new MarketingRepository(session.Transaction);
        repo.InsertDelivery(Guid.CreateVersion7(), tenantId, "LeadSubmitted", sourceId, "LeadSubmitted", "Pending", now);
        repo.InsertInbox(Guid.CreateVersion7(), eventId, "ContactStarted", now);

        var delivery = Assert.Throws<PostgresException>(() =>
            repo.InsertDelivery(Guid.CreateVersion7(), tenantId, "LeadSubmitted", sourceId, "LeadSubmitted", "Pending", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, delivery.SqlState);
    }

    [Fact]
    public void Marketing_rejects_a_duplicate_inbox_message()
    {
        var eventId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        using var session = _factory.Begin();
        var repo = new MarketingRepository(session.Transaction);
        repo.InsertInbox(Guid.CreateVersion7(), eventId, "ContactStarted", now);

        var error = Assert.Throws<PostgresException>(() =>
            repo.InsertInbox(Guid.CreateVersion7(), eventId, "ContactStarted", now));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    private void DeleteCommitted(string firstSql, string secondSql, Guid firstId, Guid secondId)
    {
        using var session = _factory.Begin();
        Db.Execute(session.Transaction, firstSql, firstId);
        Db.Execute(session.Transaction, secondSql, secondId);
        session.Commit();
    }
}
