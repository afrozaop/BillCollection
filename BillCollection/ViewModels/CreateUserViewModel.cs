using BillCollection.Models;
using System.ComponentModel.DataAnnotations;

namespace BillCollection.ViewModels
{
    public class CreateUserViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Password does not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = "Branch";

        [Display(Name = "Branch")]
        public int? BranchId { get; set; }

        public string? BranchRoutingNumber { get; set; }

        public string? Email { get; set; }

        [Display(Name = "Mobile Number")]
        public string? MobileNumber { get; set; }

        public string? Designation { get; set; }

        [Display(Name = "Employee ID")]
        public string? EmployeeId { get; set; }

        [Display(Name = "Can Edit CBS Details")]
        public bool CanEditCbsDetails { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public List<Branch> Branches { get; set; }
            = new List<Branch>();
    }
}
