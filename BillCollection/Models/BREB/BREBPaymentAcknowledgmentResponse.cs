using System.Text.Json.Serialization;

namespace BillCollection.Models.BREB
{
    public class BREBPaymentAcknowledgmentResponse
    {
        public int Status { get; set; }

        public BREBPaymentAcknowledgmentData? Data { get; set; }
    }

    public class BREBPaymentAcknowledgmentData
    {
        [JsonConverter(typeof(BREBFlexibleStringConverter))]
        public string? SMSAccountNo { get; set; }

        public string? RefNo { get; set; }

        [JsonConverter(typeof(BREBFlexibleStringConverter))]
        public string? PayerTransactionId { get; set; }

        public string? Message { get; set; }
    }
}