using Npgsql;

namespace TrackLeadFlow.Infrastructure.Persistence;

public static class Db
{
    public static void Execute(NpgsqlTransaction transaction, string sql, params object?[] args)
    {
        using var command = new NpgsqlCommand(sql, transaction.Connection, transaction);
        Add(command, args);
        command.ExecuteNonQuery();
    }

    public static bool Exists(NpgsqlTransaction transaction, string sql, params object?[] args)
    {
        using var command = new NpgsqlCommand(sql, transaction.Connection, transaction);
        Add(command, args);
        return (bool)command.ExecuteScalar()!;
    }

    public static bool Exists(NpgsqlConnection connection, string sql, params object?[] args)
    {
        using var command = new NpgsqlCommand(sql, connection);
        Add(command, args);
        return (bool)command.ExecuteScalar()!;
    }

    private static void Add(NpgsqlCommand command, object?[] args)
    {
        foreach (var arg in args)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = arg ?? DBNull.Value });
        }
    }
}
