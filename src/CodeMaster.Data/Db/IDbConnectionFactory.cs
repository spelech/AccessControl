using System.Data;

namespace CodeMaster.Data.Db;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
