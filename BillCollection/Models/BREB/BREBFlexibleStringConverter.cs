using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BillCollection.Models.BREB
{
    /// <summary>
    /// BREB sometimes returns numeric values (e.g. "ExpiresIn": 5) where our
    /// model keeps a string. This converter reads strings AND numbers into a string.
    /// </summary>
    public sealed class BREBFlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out var l)
                        ? l.ToString(CultureInfo.InvariantCulture)
                        : reader.GetDecimal().ToString(CultureInfo.InvariantCulture);
                case JsonTokenType.True:
                    return "true";
                case JsonTokenType.False:
                    return "false";
                case JsonTokenType.Null:
                    return null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(
            Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
