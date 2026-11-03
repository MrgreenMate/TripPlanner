using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;
using TripPlanner.Api.Services;

namespace TripPlanner.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserPreferenceService _prefService;

        public UsersController(AppDbContext db, UserPreferenceService prefService)
        {
            _db = db;
            _prefService = prefService;
        }

        // DTO-k 

        public class CreateUserRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? Email { get; set; }
        }

        public class UserDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        //  Összes user listázása

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAll([FromQuery] string? search)
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u =>
                    u.Name.ToLower().Contains(s) ||
                    u.Email.ToLower().Contains(s));
            }

            var list = await query
                .OrderBy(u => u.Name)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email
                })
                .ToListAsync();

            return Ok(list);
        }

        // User létrehozása / lekérése név alapján

        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateOrGetByName([FromBody] CreateUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            var normalized = req.Name.Trim();

            var existing = await _db.Users
                .FirstOrDefaultAsync(u => u.Name.ToLower() == normalized.ToLower());

            if (existing != null)
            {
                return Ok(new UserDto
                {
                    Id = existing.Id,
                    Name = existing.Name,
                    Email = existing.Email
                });
            }

            var email = string.IsNullOrWhiteSpace(req.Email)
                ? $"{normalized.ToLower().Replace(' ', '.')}.{Guid.NewGuid():N}@local"
                : req.Email;

            var user = new User
            {
                Name = normalized,
                Email = email,
                PasswordHash = "",
                PreferencesJson = null
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPreferences), new { id = user.Id }, new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email
            });
        }

       
 
        [HttpGet("{id:int}/preferences")]
        public async Task<IActionResult> GetPreferences(int id)
        {
            var prefs = await _prefService.GetForUserAsync(id);
            return Ok(prefs);
        }


        
        [HttpPut("{id:int}/preferences")]
        public async Task<IActionResult> SavePreferences(int id, [FromBody] UserPreferences prefs)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
            {
                user = new User
                {
                    Name = $"User {id}",
                    Email = $"user{id}@local",
                    PasswordHash = ""
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            await _prefService.SaveForUserAsync(user.Id, prefs);

            return Ok(new { message = "Preferences saved." });
        }
    }
}
