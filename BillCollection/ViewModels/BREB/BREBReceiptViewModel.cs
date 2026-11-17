using BillCollection.Models.BREB;

namespace BillCollection.ViewModels.BREB
{
    public class BREBReceiptViewModel
    {
        public BREBTransaction Transaction { get; set; } = null!;
        public string BranchName { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
    }
}
