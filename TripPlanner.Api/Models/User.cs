namespace TripPlanner.Api.Models
{
    public class User
    {
        public int Id { get; set; }       
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string? PreferencesJson { get; set; }
    }
}
