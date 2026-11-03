using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.DTOs;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Controllers
{
    [ApiController]
    [Route("api/pois/{poiId:int}/reviews")] 
    public class ReviewsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ReviewsController(AppDbContext db) => _db = db;

        [HttpGet] 
        public async Task<ActionResult<IEnumerable<ReviewDto>>> GetReviews(int poiId)
        {
          
            var list = await _db.Reviews
                .Where(r => r.PoiId == poiId)
                .OrderByDescending(r => r.CreatedUtc)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    PoiId = r.PoiId,
                    UserId = r.UserId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedUtc = r.CreatedUtc
                })
                .ToListAsync();

           
            if (!list.Any() && !await _db.Pois.AnyAsync(p => p.Id == poiId))
                return NotFound(new { error = "A megadott POI nem létezik." });

            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<ReviewDto>> CreateReview(int poiId, [FromBody] CreateReviewDto dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { error = "A rating 1 és 5 közé kell essen." });

            if (!await _db.Pois.AnyAsync(p => p.Id == poiId))
                return NotFound(new { error = "A megadott POI nem létezik." });

            var review = new Review
            {
                PoiId = poiId,
                UserId = dto.UserId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedUtc = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            var result = new ReviewDto
            {
                Id = review.Id,
                PoiId = review.PoiId,
                UserId = review.UserId,
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedUtc = review.CreatedUtc
            };

           
            return CreatedAtAction(nameof(GetReviews), new { poiId = poiId }, result);
        }

        [HttpGet("../rating-summary")] 
        public async Task<IActionResult> GetRatingSummary(int poiId)
        {
            var stats = await _db.Reviews
                .Where(r => r.PoiId == poiId)
                .GroupBy(r => r.PoiId)
                .Select(g => new {
                    Average = g.Average(r => r.Rating),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            if (stats == null)
            {
              
                if (!await _db.Pois.AnyAsync(p => p.Id == poiId))
                    return NotFound(new { error = "A megadott POI nem létezik." });

                return Ok(new { poiId, average = 0.0, count = 0 });
            }

            return Ok(new { poiId, average = Math.Round(stats.Average, 2), count = stats.Count });
        }
    }
}
