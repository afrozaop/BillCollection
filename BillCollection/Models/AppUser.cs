using System;
using System.Collections.Generic;

namespace BillCollection.Models;

public partial class AppUser
{
    public long Id { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? BranchRoutingNumber { get; set; }

    public int? BranchId { get; set; }

    public int? OrgRegionId { get; set; }

    public int? OrgDivisionId { get; set; }

    public string? Email { get; set; }

    public string? MobileNumber { get; set; }

    public string? Designation { get; set; }

    public string? EmployeeId { get; set; }

    public bool CanEditCbsDetails { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public virtual Branch? Branch { get; set; }
}
