using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Models;
using TripPlanner.Api.DTOs;
using Swashbuckle.AspNetCore.Filters;

namespace TripPlanner.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ItinerariesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ItinerariesController(AppDbContext db) => _db = db;


        // ============================================================
        //   🍀 1) OPTIMALIZÁLÁS – EZ MENJEN LEGELŐL A FILE-BAN!
        // ============================================================
        [HttpPost("optimize")]
        [SwaggerRequestExample(typeof(OptimizeRequest), typeof(OptimizeRequestExamples))]
        public async Task<IActionResult> Optimize([FromBody] OptimizeRequest req)
        {
            if (req.PoiIds is null || req.PoiIds.Length == 0)
                return BadRequest(new { error = "poiIds must contain at least one id." });

            var pois = await _db.Pois.Where(p => req.PoiIds.Contains(p.Id)).ToListAsync();
            if (pois.Count == 0)
                return BadRequest(new { error = "None of the provided poiIds exist." });


            // ---- STARTPOINT feloldása ----
            double startLat, startLng;
            if (req.StartPoiId.HasValue)
            {
                var sp = await _db.Pois.FindAsync(req.StartPoiId.Value);
                if (sp is null)
                    return BadRequest(new { error = $"startPoiId {req.StartPoiId} not found." });

                startLat = sp.Latitude;
                startLng = sp.Longitude;
            }
            else
            {
                startLat = req.StartLat;
                startLng = req.StartLng;
            }


            // ---- MÓD kiválasztása ----
            string mode = req.Mode?.ToLowerInvariant() ?? "distance";
            List<int> route;

            if (mode == "distance")
                route = GreedyByDistance(pois, startLat, startLng);
            else if (mode == "priority")
                route = OrderByPriority(pois, startLat, startLng);
            else // mixed
                route = GreedyMixed(
                    pois, startLat, startLng,
                    req.WeightDistance ?? 1.0,
                    req.WeightPriority ?? 1.0
                );


            // ---- SZAKASZOK + IDŐ ----
            var distKm = TotalDistanceKmIncludingStart(route, pois, startLat, startLng);
            var (segments, totalMin) = BuildSegmentsWithEta(route, pois, startLat, startLng, req.TravelMode);


            // ---- MENTÉS DB-be ----
            var itinerary = new Itinerary
            {
                UserId = req.UserId,
                PoiListJson = System.Text.Json.JsonSerializer.Serialize(req.PoiIds),
                OptimizedRouteJson = System.Text.Json.JsonSerializer.Serialize(route),
                StartLat = startLat,
                StartLng = startLng,
                TravelMode = req.TravelMode,
                CreatedUtc = DateTime.UtcNow
            };

            _db.Itineraries.Add(itinerary);
            await _db.SaveChangesAsync();


            // ---- VÁLASZ ----
            return Ok(new
            {
                itinerary.Id,
                Mode = mode,
                Route = route,
                TotalKm = Math.Round(distKm, 2),
                TotalMinutes = Math.Round(totalMin, 0),
                TravelMode = req.TravelMode,
                Segments = segments
            });
        }


        // ============================================================
        //   🍀 2) ITINERARY LISTA (GET /api/itineraries)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var all = await _db.Itineraries.OrderByDescending(i => i.CreatedUtc).ToListAsync();
            return Ok(all);
        }


        // ============================================================
        //   🍀 3) GET /api/itineraries/{id}
        // ============================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var it = await _db.Itineraries.FindAsync(id);
            return it is null ? NotFound() : Ok(it);
        }


        // ============================================================
        //   🍀 4) GET /api/itineraries/{id}/route-points
        // ============================================================
        [HttpGet("{id:int}/route-points")]
        public async Task<IActionResult> GetRoutePoints(int id)
        {
            var it = await _db.Itineraries.FindAsync(id);
            if (it is null) return NotFound();

            var ids = System.Text.Json.JsonSerializer.Deserialize<int[]>(it.OptimizedRouteJson ?? "[]")!;
            var pois = await _db.Pois.Where(p => ids.Contains(p.Id)).ToListAsync();
            var ordered = ids.Select(id2 => pois.First(p => p.Id == id2));

            return Ok(new
            {
                itineraryId = it.Id,
                points = ordered.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Latitude,
                    p.Longitude
                })
            });
        }


        // ============================================================
        //   🍀 5) ÖSSZEFOGLALÓ (distance + sorrend)
        // ============================================================
        [HttpGet("{id:int}/route-summary")]
        public async Task<IActionResult> GetRouteSummary(int id)
        {
            var it = await _db.Itineraries.FindAsync(id);
            if (it is null) return NotFound();

            var ids = System.Text.Json.JsonSerializer.Deserialize<int[]>(it.OptimizedRouteJson ?? "[]")!;
            var pois = await _db.Pois.Where(p => ids.Contains(p.Id)).ToListAsync();
            var ordered = ids.Select(id2 => pois.First(p => p.Id == id2)).ToList();

            double km = 0;
            for (int i = 1; i < ordered.Count; i++)
                km += HaversineKm(
                    ordered[i - 1].Latitude, ordered[i - 1].Longitude,
                    ordered[i].Latitude, ordered[i].Longitude);

            return Ok(new
            {
                itineraryId = it.Id,
                TotalKm = Math.Round(km, 2),
                Count = ordered.Count,
                Points = ordered.Select(p => new { p.Id, p.Name, p.Latitude, p.Longitude })
            });
        }


        // ============================================================
        //   🍀 6) GEOJSON – START + LINESTRING
        // ============================================================
        [HttpGet("{id:int}/geojson")]
        public async Task<IActionResult> GetGeoJson(int id)
        {
            var it = await _db.Itineraries.FindAsync(id);
            if (it is null) return NotFound();

            var ids = System.Text.Json.JsonSerializer.Deserialize<int[]>(it.OptimizedRouteJson ?? "[]")!;
            var pois = await _db.Pois.Where(p => ids.Contains(p.Id)).ToListAsync();
            var map = pois.ToDictionary(p => p.Id);

            var features = new List<object>();

            // start
            if (it.StartLat.HasValue && it.StartLng.HasValue)
            {
                features.Add(new
                {
                    type = "Feature",
                    geometry = new { type = "Point", coordinates = new[] { it.StartLng, it.StartLat } },
                    properties = new { kind = "start", name = "Start" }
                });
            }

            // POI-k
            int order = 1;
            foreach (int pid in ids)
            {
                var p = map[pid];
                features.Add(new
                {
                    type = "Feature",
                    geometry = new { type = "Point", coordinates = new[] { p.Longitude, p.Latitude } },
                    properties = new { order = order++, p.Id, p.Name }
                });
            }

            // vonal
            var coords = new List<double[]>();
            if (it.StartLat.HasValue && it.StartLng.HasValue)
                coords.Add(new[] { it.StartLng.Value, it.StartLat.Value });
            coords.AddRange(ids.Select(pid => new[] { map[pid].Longitude, map[pid].Latitude }));

            features.Add(new
            {
                type = "Feature",
                geometry = new { type = "LineString", coordinates = coords },
                properties = new { kind = "route" }
            });

            return Ok(new { type = "FeatureCollection", features });
        }


        // ============================================================
        //   🍀 7) SEGÉDFÜGGVÉNYEK
        // ============================================================

        private List<int> GreedyByDistance(List<Poi> pois, double lat, double lng)
        {
            var rem = new List<Poi>(pois);
            var route = new List<int>();
            double clat = lat, clng = lng;

            while (rem.Count > 0)
            {
                var next = rem.OrderBy(p => HaversineKm(clat, clng, p.Latitude, p.Longitude)).First();
                route.Add(next.Id);
                clat = next.Latitude;
                clng = next.Longitude;
                rem.Remove(next);
            }
            return route;
        }

        private List<int> OrderByPriority(List<Poi> pois, double lat, double lng)
        {
            return pois
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => HaversineKm(lat, lng, p.Latitude, p.Longitude))
                .Select(p => p.Id)
                .ToList();
        }

        private List<int> GreedyMixed(List<Poi> pois, double lat, double lng, double wDist, double wPrio)
        {
            var rem = new List<Poi>(pois);
            var route = new List<int>();
            double clat = lat, clng = lng;

            int minP = rem.Min(p => p.Priority);
            int maxP = rem.Max(p => p.Priority);
            double Norm(int v) => (maxP == minP) ? 1.0 : (v - minP) / (double)(maxP - minP);

            while (rem.Count > 0)
            {
                var next = rem.OrderBy(p =>
                {
                    double d = HaversineKm(clat, clng, p.Latitude, p.Longitude);
                    double pr = Norm(p.Priority);
                    double penalty = 1.0 - pr;
                    return wDist * d + wPrio * penalty;
                }).First();

                route.Add(next.Id);
                clat = next.Latitude;
                clng = next.Longitude;
                rem.Remove(next);
            }
            return route;
        }

        private double HaversineKm(double aLat, double aLng, double bLat, double bLng)
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

        private double TotalDistanceKmIncludingStart(List<int> route, List<Poi> pois, double sLat, double sLng)
        {
            if (route.Count == 0) return 0;

            var map = pois.ToDictionary(p => p.Id);
            double total = 0;

            // start -> első
            var first = map[route[0]];
            total += HaversineKm(sLat, sLng, first.Latitude, first.Longitude);

            // poi -> poi
            for (int i = 1; i < route.Count; i++)
            {
                var a = map[route[i - 1]];
                var b = map[route[i]];
                total += HaversineKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
            }

            return total;
        }

        private (List<object>, double) BuildSegmentsWithEta(List<int> route, List<Poi> pois,
            double sLat, double sLng, string? mode)
        {
            var map = pois.ToDictionary(p => p.Id);
            double speed = mode?.ToLower() switch
            {
                "drive" => 28.0,
                "walk" => 4.8,
                _ => 4.8
            };

            var segs = new List<object>();
            double totalMins = 0;

            // start -> first
            if (route.Count > 0)
            {
                var first = map[route[0]];
                double d = HaversineKm(sLat, sLng, first.Latitude, first.Longitude);
                double m = d / speed * 60;
                segs.Add(new
                {
                    from = "START",
                    to = new { first.Id, first.Name },
                    distanceKm = Math.Round(d, 2),
                    minutes = Math.Round(m, 0)
                });
                totalMins += m;
            }

            // poi -> poi
            for (int i = 1; i < route.Count; i++)
            {
                var a = map[route[i - 1]];
                var b = map[route[i]];
                double d = HaversineKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
                double m = d / speed * 60;

                segs.Add(new
                {
                    from = new { a.Id, a.Name },
                    to = new { b.Id, b.Name },
                    distanceKm = Math.Round(d, 2),
                    minutes = Math.Round(m, 0)
                });

                totalMins += m;
            }

            return (segs, totalMins);
        }
    }
}
