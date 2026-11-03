using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Services
{

    public class AiRouteService
    {
        private readonly AppDbContext _db;
        private readonly UserPreferenceService _prefService;

        public AiRouteService(AppDbContext db, UserPreferenceService prefService)
        {
            _db = db;
            _prefService = prefService;
        }

        public async Task<List<int>> BuildAiRouteAsync(int userId, List<Poi> pois,
            double startLat, double startLng)
        {
            if (pois.Count == 0) return new List<int>();

            var prefs = await _prefService.GetForUserAsync(userId);

            var poiIds = pois.Select(p => p.Id).ToArray();

            var reviewStats = await _db.Reviews
                .Where(r => poiIds.Contains(r.PoiId))
                .GroupBy(r => r.PoiId)
                .Select(g => new
                {
                    PoiId = g.Key,
                    AvgRating = g.Average(x => x.Rating),
                    Count = g.Count()
                })
                .ToListAsync();

            var statsMap = reviewStats.ToDictionary(
                x => x.PoiId,
                x => (x.AvgRating, x.Count)
            );

            int minPrio = pois.Min(p => p.Priority);
            int maxPrio = pois.Max(p => p.Priority);
            double NormPrio(int v)
                => maxPrio == minPrio ? 1.0 : (v - minPrio) / (double)(maxPrio - minPrio);

            double globalMinRating = reviewStats.Count > 0 ? reviewStats.Min(s => s.AvgRating) : 0.0;
            double globalMaxRating = reviewStats.Count > 0 ? reviewStats.Max(s => s.AvgRating) : 5.0;
            double NormRating(double rating)
                => globalMaxRating == globalMinRating
                    ? (rating > 0 ? 1.0 : 0.5)
                    : (rating - globalMinRating) / (globalMaxRating - globalMinRating);

            var remaining = new List<Poi>(pois);
            var route = new List<int>();

            double clat = startLat;
            double clng = startLng;

            while (remaining.Count > 0 && route.Count < prefs.MaxPoiCount)
            {
                var next = remaining
                    .OrderBy(p =>
                    {
                        
                        double dKm = HaversineKm(clat, clng, p.Latitude, p.Longitude);

                        
                        double prioNorm = NormPrio(p.Priority);

                       
                        if (!statsMap.TryGetValue(p.Id, out var s))
                        {
                            
                            double ratingNormDefault = 0.6; 
                            double trustDefault = 0.2;
                            return
                                prefs.WeightDistance * dKm +
                                prefs.WeightPriority * (1.0 - prioNorm) +
                                prefs.WeightRating * (1.0 - ratingNormDefault * trustDefault);
                        }
                        else
                        {
                            double ratingNorm = NormRating(s.AvgRating);
                            double trust = 1.0 - Math.Exp(-s.Count / 3.0); 

                            double ratingScore = ratingNorm * trust;

                            // COST: minél kisebb, annál jobb
                            double cost =
                                prefs.WeightDistance * dKm +
                                prefs.WeightPriority * (1.0 - prioNorm) +
                                prefs.WeightRating * (1.0 - ratingScore);

                            return cost;
                        }
                    })
                    .First();

                route.Add(next.Id);
                clat = next.Latitude;
                clng = next.Longitude;
                remaining.Remove(next);
            }

            return route;
        }

      
        private static double HaversineKm(double aLat, double aLng, double bLat, double bLng)
        {
            const double R = 6371.0;
            double ToRad(double v) => Math.PI * v / 180.0;
            double dLat = ToRad(bLat - aLat);
            double dLng = ToRad(bLng - aLng);

            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(aLat)) * Math.Cos(ToRad(bLat)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            return R * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        }
    }
}
