using System.Data;

namespace AccessControl.Data.Db;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
