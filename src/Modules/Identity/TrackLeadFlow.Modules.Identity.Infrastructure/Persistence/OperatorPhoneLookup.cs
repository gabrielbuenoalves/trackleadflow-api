using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Identity.Contracts.Facades;

namespace TrackLeadFlow.Modules.Identity.Infrastructure.Persistence;

public sealed class OperatorPhoneLookup : IOperatorPhones
{
    private readonly NpgsqlConnectionFactory _factory;

    public OperatorPhoneLookup(NpgsqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public IReadOnlyCollection<string> List(Guid tenantId)
    {
        using var connection = _factory.Open();
        using var command = new NpgsqlCommand(
            """
            select "Phone"
            from identity."Users"
            where "TenantId" = $1 and "Phone" is not null and "Status" = 'Active'
            """,
            connection);
        command.Parameters.Add(new NpgsqlParameter { Value = tenantId });
        using var reader = command.ExecuteReader();
        var phones = new List<string>();
        while (reader.Read())
        {
            phones.Add(reader.GetString(0));
        }

        return phones;
    }
}
