using System.Text.Json.Serialization;

namespace BillCollection.Models.BREB
{
    // "Data" part of /authenticate. A refresh-token login returns only
    // AccessToken, ExpiresIn and CreateTimestamp.
    public class BREBTokenResponse
    {
        public string? AccessToken { get; set; }
        public int ExpiresIn { get; set; }                 // minutes
        public string? CreateTimestamp { get; set; }
        public string? RefreshToken { get; set; }
        public string? RefreshTokenExpiresAt { get; set; }

        // BREB sends text such as "2 of 100"
        [JsonConverter(typeof(BREBFlexibleStringConverter))]
        public string? AuthenticationLimit { get; set; }
    }
}
