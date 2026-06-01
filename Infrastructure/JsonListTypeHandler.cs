using Dapper;
using Newtonsoft.Json;
using System.Data;

namespace PottaAPI.Infrastructure
{
    /// <summary>
    /// Dapper type handler for List of string properties that are stored as JSON in the database
    /// </summary>
    public class JsonListTypeHandler : SqlMapper.TypeHandler<List<string>>
    {
        public override List<string> Parse(object value)
        {
            if (value == null || value is DBNull)
                return new List<string>();

            var json = value.ToString();
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                // If deserialization fails, return empty list
                return new List<string>();
            }
        }

        public override void SetValue(IDbDataParameter parameter, List<string>? value)
        {
            parameter.Value = value == null || value.Count == 0
                ? (object)DBNull.Value
                : JsonConvert.SerializeObject(value);
        }
    }
}
