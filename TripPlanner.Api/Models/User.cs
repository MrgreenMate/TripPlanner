namespace TripPlanner.Api.Models
{
    public class User
    {
        public int Id { get; set; }            // User_ID a terv szerint
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string? PreferencesJson { get; set; } // egyszerű JSON tárolás kezdetnek
    }
}
