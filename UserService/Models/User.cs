namespace UserService.Models
{
    public class User
    {
        public int Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string CPR { get; set; }

        public string TLF { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }

        public bool IsActive { get; set; } = true;
    }
}