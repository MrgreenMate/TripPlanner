using Microsoft.AspNetCore.Mvc;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly AppDbContext _db;
    public DevController(AppDbContext db) => _db = db;

    [HttpPost("seed-obudai")]
    public async Task<IActionResult> SeedObudai()
    {
        if (_db.Pois.Any())
            return Ok(new { message = "Already seeded", count = _db.Pois.Count() });

        _db.Pois.AddRange(
            new Poi { Name = "Óbudai Egyetem NIK", Latitude = 47.5391, Longitude = 19.0375, Description = "Bécsi út 96/B", Priority = 3 },
            new Poi { Name = "Óbudai Egyetem KVK", Latitude = 47.4898, Longitude = 19.0728, Description = "Tavaszmező 15–17.", Priority = 2 },
            new Poi { Name = "Óbudai Egyetem YBL", Latitude = 47.5079, Longitude = 19.0865, Description = "Thököly út 74.", Priority = 1 }
        );
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Seeded",
            ids = _db.Pois.Select(p => new { p.Id, p.Name }).ToList()
        });
    }
}
