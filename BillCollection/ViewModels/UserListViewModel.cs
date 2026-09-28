
using BillCollection.Models;

namespace BillCollection.Models.ViewModels
{
    public class UserListViewModel
    {
        public List<AppUser> Users { get; set; } = new();

        public string? Search { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalRecords { get; set; }

        public int TotalPages =>
            (int)Math.Ceiling((double)TotalRecords / PageSize);
    }
}

