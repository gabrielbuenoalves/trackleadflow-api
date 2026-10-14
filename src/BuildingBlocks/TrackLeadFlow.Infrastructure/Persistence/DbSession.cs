using Npgsql;

namespace TrackLeadFlow.Infrastructure.Persistence;

public sealed class DbSession : IDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _completed;

    public DbSession(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public NpgsqlTransaction Transaction => _transaction;

    public void Commit()
    {
        _transaction.Commit();
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            try
            {
                _transaction.Rollback();
            }
            catch (PostgresException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        _transaction.Dispose();
        _connection.Dispose();
    }
}
