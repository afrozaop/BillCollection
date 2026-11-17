using System.Text.Json.Serialization;

namespace BillCollection.Models.BREB
{
    public class BREBPaymentSubmitResponse
    {
        [JsonConverter(typeof(BREBFlexibleStringConverter))]
        public string? SMSAccountNo { get; set; }

        public string? RefNo { get; set; }

        [JsonConverter(typeof(BREBFlexibleStringConverter))]
        public string? PayerTransactionId { get; set; }

        [JsonConverter(typeof(BREBFlexibleStringConverter))]
        public string? ExpiresAt { get; set; }

        public string? Message { get; set; }

        public bool Success { get; set; }
    }
}