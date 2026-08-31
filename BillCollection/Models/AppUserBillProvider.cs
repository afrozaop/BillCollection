using System;
using System.Collections.Generic;

namespace BillCollection.Models;

public partial class AppUserBillProvider
{
    public long Id { get; set; }

    public long AppUserId { get; set; }

    public int ProviderId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? CreatedBy { get; set; }

    public virtual AppUser AppUser { get; set; } = null!;

    public virtual BillProvider Provider { get; set; } = null!;
}
