namespace BillCollection.ViewModels.BREB
{
    public class BREBBillResultViewModel
    {
        public bool Success { get; set; }

        public string? Message { get; set; }

        public string? SMSAccountNo { get; set; }

        public string? RefNo { get; set; }

        public string? PaymentTimestamp { get; set; }

        public string? DueType { get; set; }

        public string? BillMonth { get; set; }

        public decimal Amount { get; set; }

        public decimal Vat { get; set; }

        public string? ExpiresIn { get; set; }
    }
}