
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyFirstMVCApp.Models
{
    public class LeaveBalance
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee Employee { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string LeaveType { get; set; } = string.Empty;

        [Range(0, 366)]
        public int AnnualAllowance { get; set; }

        [Range(0, 366)]
        public int UsedDays { get; set; }

        [Required]
        public int Year { get; set; }

        [NotMapped]
        public int RemainingDays => AnnualAllowance - UsedDays;
    }
}
