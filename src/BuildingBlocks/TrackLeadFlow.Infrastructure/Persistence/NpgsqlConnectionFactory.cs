using Npgsql;

namespace TrackLeadFlow.Infrastructure.Persistence;

public sealed class NpgsqlConnectionFactory
{
    private readonly string? _databaseUrl;

    public NpgsqlConnectionFactory(string? databaseUrl = null)
    {
        _databaseUrl = databaseUrl;
    }

    public NpgsqlConnection Open()
    {
        var url = _databaseUrl ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("DATABASE_URL is not set.");
        }

        var connection = new NpgsqlConnection(ToConnectionString(url));
        connection.Open();
        return connection;
    }

    public DbSession Begin()
    {
        var connection = Open();
        return new DbSession(connection, connection.BeginTransaction());
    }

    public static string ToConnectionString(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var separator = uri.UserInfo.IndexOf(':');
        if (separator <= 0 || separator == uri.UserInfo.Length - 1)
        {
            throw new InvalidOperationException("DATABASE_URL is missing credentials.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(uri.UserInfo[..separator]),
            Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]),
            SslMode = SslMode.Require,
        };
        return builder.ConnectionString;
    }
}
