using System.Text.Json;
using System.Text.Json.Serialization;

namespace BillCollection.Services
{
    internal static class BREBJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,          // spec uses PascalCase, be tolerant
            PropertyNamingPolicy = null,                 // send PascalCase exactly as in spec
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
    }
}
