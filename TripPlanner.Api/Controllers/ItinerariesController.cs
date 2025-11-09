using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;

[ApiController]
[Route("api/[controller]")]
public class ItinerariesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ItinerariesController(AppDbContext db) => _db = db;

    // --- már létező Optimize metódusod marad ---

    // GET: /api/itineraries/{id}/route-points
    [HttpGet("{id:int}/route-points")]
    public async Task<IActionResult> GetRoutePoints(int id)
    {
        var it = await _db.Itineraries.FindAsync(id);
        if (it is null) return NotFound();

        var poiIds = System.Text.Json.JsonSerializer.Deserialize<int[]>(it.OptimizedRouteJson ?? "[]") ?? [];
        var pois = await _db.Pois.Where(p => poiIds.Contains(p.Id)).ToListAsync();

        var ordered = poiIds
            .Select(pid => pois.FirstOrDefault(p => p.Id == pid))
            .Where(p => p != null)
            .Select(p => new { id = p!.Id, name = p!.Name, latitude = p!.Latitude, longitude = p!.Longitude })
            .ToList();

        return Ok(new { itineraryId = it.Id, count = ordered.Count, points = ordered });
    }

    // GET: /api/itineraries/{id}/route-summary  (össztáv km-ben)
    [HttpGet("{id:int}/route-summary")]
    public async Task<IActionResult> GetRouteSummary(int id)
    {
        var it = await _db.Itineraries.FindAsync(id);
        if (it is null) return NotFound();

        var poiIds = System.Text.Json.JsonSerializer.Deserialize<int[]>(it.OptimizedRouteJson ?? "[]") ?? [];
        var pois = await _db.Pois.Where(p => poiIds.Contains(p.Id)).ToListAsync();

        var ordered = poiIds
            .Select(pid => pois.FirstOrDefault(p => p.Id == pid))
            .Where(p => p != null)
            .Select(p => new { id = p!.Id, name = p!.Name, latitude = p!.Latitude, longitude = p!.Longitude })
            .ToList();

        double totalKm = 0;
        for (int i = 1; i < ordered.Count; i++)
            totalKm += HaversineKm(ordered[i - 1].latitude, ordered[i - 1].longitude,
                                   ordered[i].latitude, ordered[i].longitude);

        return Ok(new { itineraryId = it.Id, count = ordered.Count, totalKm = Math.Round(totalKm, 2), points = ordered });
    }

    // --- ha itt még nincs Haversine-ed, add hozzá (vagy tedd public static-ként máshova) ---
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double ToRad(double d) => Math.PI * d / 180.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
