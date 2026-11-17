
using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs
{
    public class CreateUserDto
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string CPR { get; set; }

        [Required]
        public string TLF { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
