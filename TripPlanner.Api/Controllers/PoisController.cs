using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;

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
}
