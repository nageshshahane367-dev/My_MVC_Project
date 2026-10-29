
using System.ComponentModel.DataAnnotations;

namespace MyFirstMVCApp.Dto
{
    public class LeaveRequestDto
    {
        [Required(ErrorMessage = "Please select a leave type.")]
        [StringLength(30)]
        [Display(Name = "Leave Type")]
        [RegularExpression(
            "^(Casual|Sick|Earned)$",
            ErrorMessage = "Leave type must be Casual, Sick, or Earned.")]
        public string LeaveType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a start date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Please select an end date.")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Please provide a reason for leave.")]
        [StringLength(
            1000,
            MinimumLength = 5,
            ErrorMessage = "The reason must be between 5 and 1000 characters.")]
        [Display(Name = "Reason for Leave")]
        public string Reason { get; set; } = string.Empty;
    }
}
