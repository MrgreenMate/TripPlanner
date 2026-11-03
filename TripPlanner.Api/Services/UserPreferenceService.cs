using System.Text.Json;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace TripPlanner.Api.Services
{
    public class UserPreferenceService
    {
        private readonly AppDbContext _db;

        public UserPreferenceService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<UserPreferences> GetForUserAsync(int userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || string.IsNullOrWhiteSpace(user.PreferencesJson))
                return new UserPreferences(); 

            try
            {
                var prefs = JsonSerializer.Deserialize<UserPreferences>(user.PreferencesJson);
                return prefs ?? new UserPreferences();
            }
            catch
            {
           
                return new UserPreferences();
            }
        }

        public async Task SaveForUserAsync(int userId, UserPreferences prefs)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return;

            user.PreferencesJson = JsonSerializer.Serialize(prefs);
            await _db.SaveChangesAsync();
        }
    }
}
