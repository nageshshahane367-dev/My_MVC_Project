
using System.ComponentModel.DataAnnotations;

namespace MyFirstMVCApp.Dto
{
    public class EmployeeDescriptionRequest
    {
        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Designation { get; set; } = string.Empty;
    }
}
