using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.Qualification.Infrastructure.Persistence;

public sealed class QualificationRepository
{
    private readonly NpgsqlTransaction _transaction;

    public QualificationRepository(NpgsqlTransaction transaction)
    {
        _transaction = transaction;
    }

    public void InsertCriteriaSet(Guid id, Guid tenantId, string name, bool isActive, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into qualification."CriteriaSets"
                ("Id", "TenantId", "Name", "Version", "IsActive", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, 1, $4, $5, $5)
            """,
            id,
            tenantId,
            name,
            isActive,
            now);
    }

    public void InsertStep(Guid id, Guid criteriaSetId, int sortOrder, string kind, string prompt)
    {
        Db.Execute(
            _transaction,
            """
            insert into qualification."Steps" ("Id", "CriteriaSetId", "SortOrder", "Kind", "Prompt", "ConfigJson")
            values ($1, $2, $3, $4, $5, '{}')
            """,
            id,
            criteriaSetId,
            sortOrder,
            kind,
            prompt);
    }

    public void InsertLead(Guid id, Guid tenantId, Guid conversationId, Guid criteriaSetId, string status, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into qualification."Leads"
                ("Id", "TenantId", "ConversationId", "CriteriaSetId", "Status", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, $6, $6)
            """,
            id,
            tenantId,
            conversationId,
            criteriaSetId,
            status,
            now);
    }

    public void InsertAnswer(Guid id, Guid leadId, Guid stepId, string rawValue, DateTimeOffset answeredAt)
    {
        Db.Execute(
            _transaction,
            """
            insert into qualification."LeadAnswers" ("Id", "LeadId", "StepId", "RawValue", "AnsweredAt")
            values ($1, $2, $3, $4, $5)
            """,
            id,
            leadId,
            stepId,
            rawValue,
            answeredAt);
    }

    public bool LeadExists(Guid id) =>
        Db.Exists(_transaction, """select exists(select 1 from qualification."Leads" where "Id" = $1)""", id);

    public bool AnswerExists(Guid id) =>
        Db.Exists(_transaction, """select exists(select 1 from qualification."LeadAnswers" where "Id" = $1)""", id);
}
