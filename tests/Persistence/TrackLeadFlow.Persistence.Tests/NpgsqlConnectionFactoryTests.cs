using TrackLeadFlow.Infrastructure.Persistence;
using Xunit;

namespace TrackLeadFlow.Persistence.Tests;

public sealed class NpgsqlConnectionFactoryTests
{
    [Fact]
    public void Open_fails_when_database_url_is_missing()
    {
        var previous = Environment.GetEnvironmentVariable("DATABASE_URL");
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        try
        {
            var factory = new NpgsqlConnectionFactory();
            var error = Assert.Throws<InvalidOperationException>(() => factory.Open());
            Assert.Contains("DATABASE_URL", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", previous);
        }
    }

    [Fact]
    public void Open_connects_and_closes()
    {
        var factory = new NpgsqlConnectionFactory();
        using var connection = factory.Open();
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        connection.Close();
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }
}
