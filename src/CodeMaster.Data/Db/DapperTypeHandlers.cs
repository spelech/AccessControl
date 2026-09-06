using System.Data;
using Dapper;

namespace CodeMaster.Data.Db;

public static class DapperTypeHandlers
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
            SqlMapper.AddTypeHandler(new NullableTimeOnlyTypeHandler());
        }
    }
}

public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.Value = value.ToString("HH:mm:ss");
    }

    public override TimeOnly Parse(object value)
    {
        if (value is string s && TimeOnly.TryParse(s, out var time))
        {
            return time;
        }
        if (value is DateTime dt)
        {
            return TimeOnly.FromDateTime(dt);
        }
        if (value is TimeSpan ts)
        {
            return TimeOnly.FromTimeSpan(ts);
        }
        return default;
    }
}

public class NullableTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly?>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly? value)
    {
        if (value.HasValue)
        {
            parameter.Value = value.Value.ToString("HH:mm:ss");
        }
        else
        {
            parameter.Value = DBNull.Value;
        }
    }

    public override TimeOnly? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }
        if (value is string s && TimeOnly.TryParse(s, out var time))
        {
            return time;
        }
        if (value is DateTime dt)
        {
            return TimeOnly.FromDateTime(dt);
        }
        if (value is TimeSpan ts)
        {
            return TimeOnly.FromTimeSpan(ts);
        }
        return null;
    }
}
