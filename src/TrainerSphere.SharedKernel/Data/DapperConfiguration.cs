using System.Data;
using Dapper;

namespace TrainerSphere.SharedKernel.Data;

public static class DapperConfiguration
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1)
        {
            return;
        }

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => throw new DataException($"Cannot convert {value.GetType()} to DateOnly."),
        };

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            parameter.DbType = DbType.Date;
            parameter.Value = value;
        }
    }
}
