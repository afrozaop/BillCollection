using System;
using System.Collections.Generic;

namespace BillCollection.Models;

public partial class BillProvider
{
    public int Id { get; set; }

    public string ProviderCode { get; set; } = null!;

    public string ProviderName { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual ICollection<BranchBillProvider> BranchBillProviders { get; set; } = new List<BranchBillProvider>();
}
