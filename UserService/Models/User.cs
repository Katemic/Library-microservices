
namespace UserService.Models
{
    public class User
    {
        // Unique ID in the UserService database.
        public int Id { get; set; }

        // Unique user ID from Keycloak (JWT "sub" claim).
        // Existing users can have NULL until they are linked to Keycloak.
        public string? KeycloakSubject { get; set; }

        // Library user profile information.
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        // Optional legacy field.
        // CPR is not required for standard Keycloak registration.
        public string? CPR { get; set; }

        public string TLF { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // Passwords are handled by Keycloak.
        // UserService does not store user passwords.

        // Indicates whether the library profile is active.
        public bool IsActive { get; set; } = true;
    }
}
