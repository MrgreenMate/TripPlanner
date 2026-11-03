using System.Text.Json;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Services
{
    public class PreferenceService
    {
        private readonly AppDbContext _db;

        public PreferenceService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<UserPreferences> GetPreferencesAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);

            if (user == null || string.IsNullOrWhiteSpace(user.PreferencesJson))
                return new UserPreferences(); 

            try
            {
                return JsonSerializer.Deserialize<UserPreferences>(user.PreferencesJson)
                    ?? new UserPreferences();
            }
            catch
            {
                return new UserPreferences();
            }
        }
    }
}
