using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs
{
    public class UpdateEmailDto
    {
        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string Email { get; set; } = string.Empty;
    }
}
