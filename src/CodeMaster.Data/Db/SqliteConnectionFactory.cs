using System.Data;
using Microsoft.Data.Sqlite;

namespace CodeMaster.Data.Db;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    static SqliteConnectionFactory()
    {
        DapperTypeHandlers.Register();
    }

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public IDbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }

        return connection;
    }
}
