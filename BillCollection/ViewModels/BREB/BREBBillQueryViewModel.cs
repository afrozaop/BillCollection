using System.ComponentModel.DataAnnotations;

namespace BillCollection.ViewModels.BREB
{
  
        public class BREBBillQueryViewModel
        {
            [Required]
            [Display(Name = "SMS Account Number")]
            public string SMSAccountNo { get; set; } = string.Empty;

            [Required]
            public int BillType { get; set; } = 50;

            public string? PayerId { get; set; }

            public int PayerTypeId { get; set; } = 2;
        }
    }

