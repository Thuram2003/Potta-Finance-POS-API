using Dapper;
using System.Data;

namespace PottaAPI.Infrastructure
{
    /// <summary>
    /// Dapper type handler for decimal properties that are stored as REAL (double) in SQLite
    /// </summary>
    public class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
    {
        public override decimal Parse(object value)
        {
            if (value == null || value is DBNull)
                return 0m;

            // SQLite stores numbers as double, convert to decimal
            return Convert.ToDecimal(value);
        }

        public override void SetValue(IDbDataParameter parameter, decimal value)
        {
            parameter.Value = value;
        }
    }

    /// <summary>
    /// Dapper type handler for nullable decimal properties
    /// </summary>
    public class NullableDecimalTypeHandler : SqlMapper.TypeHandler<decimal?>
    {
        public override decimal? Parse(object value)
        {
            if (value == null || value is DBNull)
                return null;

            return Convert.ToDecimal(value);
        }

        public override void SetValue(IDbDataParameter parameter, decimal? value)
        {
            parameter.Value = value.HasValue ? (object)value.Value : DBNull.Value;
        }
    }
}
