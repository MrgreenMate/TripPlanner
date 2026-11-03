using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;
using TripPlanner.Api.Services;

[ApiController]
[Route("api/[controller]")]
public class PoisController : ControllerBase
{
    private readonly AppDbContext _db;
    public PoisController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _db.Pois.ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var poi = await _db.Pois.FindAsync(id);
        return poi is null ? NotFound() : Ok(poi);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Poi poi)
    {
        _db.Pois.Add(poi);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = poi.Id }, poi);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Poi updated)
    {
        if (id != updated.Id) return BadRequest();
        _db.Entry(updated).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.Pois.FindAsync(id);
        if (e is null) return NotFound();
        _db.Pois.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }
    [HttpGet("nearby")]
    public async Task<ActionResult<IEnumerable<Poi>>> GetNearby(
    double lat,
    double lng,
    double radiusKm = 3,
    int maxCount = 20)
    {
        var all = await _db.Pois.ToListAsync();

        var list = all
            .Select(p => new
            {
                Poi = p,
                Dist = HaversineKm(lat, lng, p.Latitude, p.Longitude)
            })
            .Where(x => x.Dist <= radiusKm)
            .OrderBy(x => x.Dist)
            .Take(maxCount)
            .Select(x => x.Poi)
            .ToList();

        return Ok(list);
    }



    [HttpPost("auto-import")]
    public async Task<IActionResult> AutoImportNearby(
     [FromQuery] double lat,
     [FromQuery] double lng,
     [FromServices] OverpassService overpass,
     [FromServices] AiEnrichmentService aiEnrichment)
    {
        var rawPois = await overpass.GetNearbyPoisAsync(lat, lng, radiusMeters: 3000);

        if (!rawPois.Any())
            return NotFound(new { message = "Nem találtunk látványosságot a megadott területen." });


        var existingNames = _db.Pois.Select(p => p.Name.ToLower()).ToList();
        var newRawPois = rawPois.Where(p => !existingNames.Contains(p.Name.ToLower())).ToList();

        if (!newRawPois.Any())
            return Ok(new { message = "Minden közeli helyszín már szerepel az adatbázisban." });

     
        var enrichedDataList = await aiEnrichment.EnrichPoisWithReviewsAsync(newRawPois);

        int newPoisCount = 0;
        int newReviewsCount = 0;

  
        foreach (var item in enrichedDataList)
        {
           
            _db.Pois.Add(item.Poi);
            await _db.SaveChangesAsync();
            newPoisCount++;

           
            if (item.Reviews.Any())
            {
                foreach (var review in item.Reviews)
                {
                    review.PoiId = item.Poi.Id;
                    _db.Reviews.Add(review);
                    newReviewsCount++;
                }
                await _db.SaveChangesAsync();
            }
        }

        return Ok(new
        {
            message = "Sikeres automatikus import értékelésekkel együtt!",
            importedPois = newPoisCount,
            importedReviews = newReviewsCount
        });
    }
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180.0)
            * Math.Cos(lat2 * Math.PI / 180.0)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

}
