using System;
using System.Collections.Generic;

namespace BillCollection.Models;

public partial class Branch
{
    public int Id { get; set; }

    public string RoutingNumber { get; set; } = null!;

    public string BranchCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? NameBangla { get; set; }

    public int OrgRegionId { get; set; }

    public string? Address { get; set; }

    public string? District { get; set; }

    public string? PoliceStation { get; set; }

    public string? ContactNumber { get; set; }

    public string? Email { get; set; }

    public string? ManagerName { get; set; }

    public bool IsAdBranch { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual ICollection<AppUser> AppUsers { get; set; } = new List<AppUser>();

    public virtual ICollection<BranchBillProvider> BranchBillProviders { get; set; } = new List<BranchBillProvider>();
}
