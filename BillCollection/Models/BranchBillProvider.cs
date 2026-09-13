namespace BillCollection.Models;

public partial class BranchBillProvider
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public int ProviderId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? CreatedBy { get; set; }

    // Navigation Properties
    public virtual Branch Branch { get; set; } = null!;

    public virtual BillProvider Provider { get; set; } = null!;
}