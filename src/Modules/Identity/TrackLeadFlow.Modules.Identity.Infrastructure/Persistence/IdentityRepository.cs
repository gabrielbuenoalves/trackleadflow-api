using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityRepository
{
    private readonly NpgsqlTransaction _transaction;

    public IdentityRepository(NpgsqlTransaction transaction)
    {
        _transaction = transaction;
    }

    public void InsertTenant(Guid id, string name, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into identity."Tenants" ("Id", "Name", "Status", "CreatedAt", "UpdatedAt")
            values ($1, $2, 'Active', $3, $3)
            """,
            id,
            name,
            now);
    }

    public void InsertUser(Guid id, Guid tenantId, string email, string displayName, string? phone, string passwordHash, DateTimeOffset now)
    {
        Db.Execute(
            _transaction,
            """
            insert into identity."Users"
                ("Id", "TenantId", "Email", "DisplayName", "Phone", "PasswordHash", "Status", "CreatedAt", "UpdatedAt")
            values ($1, $2, $3, $4, $5, $6, 'Active', $7, $7)
            """,
            id,
            tenantId,
            email,
            displayName,
            phone,
            passwordHash,
            now);
    }

    public void InsertRole(Guid userId, string role)
    {
        Db.Execute(
            _transaction,
            """insert into identity."UserRoles" ("UserId", "Role") values ($1, $2)""",
            userId,
            role);
    }

    public bool UserExists(Guid id) =>
        Db.Exists(_transaction, """select exists(select 1 from identity."Users" where "Id" = $1)""", id);

    public bool RoleExists(Guid userId, string role) =>
        Db.Exists(_transaction, """select exists(select 1 from identity."UserRoles" where "UserId" = $1 and "Role" = $2)""", userId, role);
}
