using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Services
{
    public class AiScoringService
    {
        private readonly AppDbContext _db;

        public AiScoringService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<double> ScoreCandidateAsync(
            int poiId,
            double distanceKm,
            UserPreferences prefs)
        {
            var poi = await _db.Pois.FindAsync(poiId);
            if (poi == null)
                return double.NegativeInfinity;

        
            double prioScore = poi.Priority / 10.0;

         
            double avgRating = 0;
            var reviews = _db.Reviews.Where(r => r.PoiId == poiId);
            int count = await reviews.CountAsync();
            if (count > 0)
                avgRating = (await reviews.AverageAsync(r => r.Rating)) / 5.0;

   
            double distScore = 1.0 / (1.0 + distanceKm);

            return distScore * prefs.WeightDistance +
                    prioScore * prefs.WeightPriority +
                    avgRating * prefs.WeightRating;
        }
    }
}
