using System;
using System.Collections.Generic;

namespace BillCollection.Models;

public partial class OrgRegion
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int OrgDivisionId { get; set; }

    public string? Address { get; set; }

    public string? ContactNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual ICollection<AppUser> AppUsers { get; set; } = new List<AppUser>();

    public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();

    public virtual OrgDivision OrgDivision { get; set; } = null!;
}
