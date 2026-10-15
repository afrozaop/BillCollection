namespace BillCollection.Models.BREB
{
    public class BREBBillingInfoRequest
    {
        public string SMSAccountNo { get; set; } = string.Empty;

        public int BillType { get; set; } = 50;

        public string? PayerId { get; set; }

        public int PayerTypeId { get; set; } = 2;

        public string? BranchRoutingNo { get; set; }

        public string? AgentId { get; set; }
    }
}