using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DevController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpPost("import-pois")]
    public async Task<IActionResult> ImportPois()
    {
        
        var filePath = Path.Combine(_env.ContentRootPath, "data", "pois.json");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = $"A fájl nem található: {filePath}" });
        }

        var json = await System.IO.File.ReadAllTextAsync(filePath);

        List<Poi>? imported;
        try
        {
            imported = JsonSerializer.Deserialize<List<Poi>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Hibás JSON formátum", detail = ex.Message });
        }

        if (imported == null || imported.Count == 0)
        {
            return BadRequest(new { error = "A JSON nem tartalmaz egyetlen POI-t sem." });
        }

       
        var existingNames = new HashSet<string>(
            _db.Pois.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase
        );

        var toAdd = imported
            .Where(p => !existingNames.Contains(p.Name))
            .ToList();

        if (toAdd.Count == 0)
        {
            return Ok(new { message = "Nem volt új POI a fájlban.", totalExisting = existingNames.Count });
        }

        _db.Pois.AddRange(toAdd);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "POI-k importálva",
            added = toAdd.Count,
            total = _db.Pois.Count()
        });
    }
    [HttpPost("import-reviews")]
    public async Task<IActionResult> ImportReviews([FromBody] List<Review> list)
    {
        _db.Reviews.AddRange(list);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Review import sikeres!" });
    }
}
