namespace BillCollection.ViewModels.BREB
{
    public class BREBBillCollectionViewModel
    {
        public string SMSAccountNo { get; set; } = string.Empty;

        // Official Receipt = 10
        // Application Fee = 19
        // Demand Note = 20
        // Advance Electricity Bill = 30
        // Electricity Bill = 50
        public int BillType { get; set; } = 50;

        // Agent_Bank = 2
        public int PayerTypeId { get; set; } = 2;

        public string? PayerId { get; set; }
        public int? TransactionId { get; set; }
    }
}