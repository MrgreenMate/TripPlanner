using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TripPlanner.Api.Data;
using TripPlanner.Api.DTOs;
using TripPlanner.Api.Models;
using TripPlanner.Api.Services;


namespace TripPlanner.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ItinerariesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PreferenceService _preferences;
        private readonly AiScoringService _ai;
        private readonly AiRouteService _aiRoute;
        private readonly OpenRouteService _ors; 

        public ItinerariesController(
            AppDbContext db,
            PreferenceService preferences,
            AiScoringService ai,
            AiRouteService aiRoute,
            OpenRouteService ors) 
        {
            _db = db;
            _preferences = preferences;
            _ai = ai;
            _aiRoute = aiRoute;
            _ors = ors; 
        }



        // 1) OPTIMALIZÁLÁS 

        [HttpPost("optimize")]
        public async Task<ActionResult<Itinerary>> Optimize([FromBody] OptimizeRequest req)
        {
            // POI lista betöltése
            var pois = await _db.Pois
                .Where(p => req.PoiIds.Contains(p.Id))
                .ToListAsync();

            if (pois.Count == 0)
                return BadRequest(new { error = "Nincs feldolgozható POI." });

           
            double startLat = req.StartLat;
            double startLng = req.StartLng;

            if (req.StartPoiId != null)
            {
                var startPoi = await _db.Pois.FindAsync(req.StartPoiId);
                if (startPoi == null)
                    return BadRequest(new { error = "StartPoiId érvénytelen." });

                startLat = startPoi.Latitude;
                startLng = startPoi.Longitude;
            }

          
            string mode = req.Mode?.ToLowerInvariant() ?? "distance";
            List<int> route;

          
            if (mode == "genetic")//alapeset
            {
                route = GeneticAlgorithm(pois, startLat, startLng);
            }
            else if (mode == "ai") //régi megoldások 
            {
                route = await GreedyAiAsync(pois, startLat, startLng, req.UserId);
            }
            else if (mode == "distance")
            {
                route = GreedyByDistance(pois, startLat, startLng);
            }
            else if (mode == "priority")
            {
                route = OrderByPriority(pois, startLat, startLng);
            }
            else //Ha egyik se fut le
            {
                route = GreedyMixed(
                    pois,
                    startLat,
                    startLng,
                    req.WeightDistance ?? 1.0,
                    req.WeightPriority ?? 1.0
                );
            }

            // POI-k sorrendjének elmentése JSON-ben
            var itinerary = new Itinerary
            {
                UserId = req.UserId,
                StartLat = startLat,
                StartLng = startLng,
                TravelMode = req.TravelMode,
                PoiListJson = JsonSerializer.Serialize(req.PoiIds),
                OptimizedRouteJson = JsonSerializer.Serialize(route)
            };

            _db.Itineraries.Add(itinerary);
            await _db.SaveChangesAsync();

            return Ok(new { id = itinerary.Id });
        }



        //   2) ITINERARY LISTA 

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var all = await _db.Itineraries.OrderByDescending(i => i.CreatedUtc).ToListAsync();
            return Ok(all);
        }



        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var it = await _db.Itineraries.FindAsync(id);
            return it is null ? NotFound() : Ok(it);
        }



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


        [HttpGet("{id:int}/geojson")]
        public async Task<IActionResult> GetGeoJson(int id)
        {
            var it = await _db.Itineraries.FindAsync(id);
            if (it is null) return NotFound();

            var ids = System.Text.Json.JsonSerializer.Deserialize<int[]>(it.OptimizedRouteJson ?? "[]")!;
            var pois = await _db.Pois.Where(p => ids.Contains(p.Id)).ToListAsync();
            var map = pois.ToDictionary(p => p.Id);

            var features = new List<object>();

            // Start pont
            if (it.StartLat.HasValue && it.StartLng.HasValue)
            {
                features.Add(new
                {
                    type = "Feature",
                    geometry = new { type = "Point", coordinates = new[] { it.StartLng.Value, it.StartLat.Value } },
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

            var routeCoords = new List<double[]>();
            if (it.StartLat.HasValue && it.StartLng.HasValue)
                routeCoords.Add(new[] { it.StartLng.Value, it.StartLat.Value }); 

            routeCoords.AddRange(ids.Select(pid => new[] { map[pid].Longitude, map[pid].Latitude })); 

            // Valós útvonal lekérése

            var travelMode = string.IsNullOrEmpty(it.TravelMode) ? "walk" : it.TravelMode;
            var realGeometry = await _ors.GetRealRouteGeometryAsync(routeCoords, travelMode);

            if (realGeometry.HasValue)
            {

                features.Add(new
                {
                    type = "Feature",
                    geometry = realGeometry.Value,
                    properties = new { kind = "route", description = "Valós útvonal" }
                });
            }
            else
            {
                // FALLBACK: Ha nincs API kulcs vagy hiba vanlégvonalbeli (Haversine) kirajzolás
                features.Add(new
                {
                    type = "Feature",
                    geometry = new { type = "LineString", coordinates = routeCoords },
                    properties = new { kind = "route", description = "Légvonal" }
                });
            }

            return Ok(new { type = "FeatureCollection", features });
        }
        [HttpPost("discover-optimize")]
        public async Task<ActionResult<Itinerary>> DiscoverAndOptimize(DiscoverOptimizeRequest req)
        {
     
            var all = await _db.Pois.ToListAsync();

            var candidates = all
                .Select(p => new
                {
                    Poi = p,
                    Dist = HaversineKm(req.StartLat, req.StartLng, p.Latitude, p.Longitude)
                })
                .Where(x => x.Dist <= req.RadiusKm)
                .ToList();

            if (!candidates.Any())
                return BadRequest(new { error = "Nincsenek POI-k a megadott sugárban." });

          
            var scored = new List<(Poi poi, double score)>();
            var prefs = await _preferences.GetPreferencesAsync(req.UserId);

            foreach (var c in candidates)
            {
                double aiScore = await _ai.ScoreCandidateAsync(c.Poi.Id, c.Dist, prefs);
                scored.Add((c.Poi, aiScore));
            }

    
            var selectedPois = scored
                .OrderByDescending(s => s.score)
                .Take(req.MaxPois)
                .Select(s => s.poi)
                .ToList();

           
            var optimizeReq = new OptimizeRequest
            {
                UserId = req.UserId,
                StartLat = req.StartLat,
                StartLng = req.StartLng,
                PoiIds = selectedPois.Select(p => p.Id).ToArray(),
                Mode = "genetic", 
                TravelMode = req.TravelMode 
            };

            var itinerary = await OptimizeInternal(optimizeReq);

            return Ok(itinerary);
        }

        //   SEGÉDFÜGGVÉNYEK


        // --- GENETIKUS ALGORITMUS ---
        private List<int> GeneticAlgorithm(List<Poi> pois, double startLat, double startLng)
        {
            
            int popSize = 50;       
            int generations = 100;  
            var random = new Random();
            var population = new List<List<int>>();

           
            var baseRoute = pois.Select(p => p.Id).ToList();
            for (int i = 0; i < popSize; i++)
            {
                var shuffled = baseRoute.OrderBy(x => random.Next()).ToList();
                population.Add(shuffled);
            }

          
            for (int g = 0; g < generations; g++)
            {
                
                var fitnessList = population.Select(route => new
                {
                    Route = route,
                    Fitness = 1.0 / CalculateRouteDistance(route, pois, startLat, startLng)
                }).ToList();

           
                fitnessList = fitnessList.OrderByDescending(x => x.Fitness).ToList();
                var newPopulation = fitnessList.Take(10).Select(x => x.Route).ToList();

              
                while (newPopulation.Count < popSize)
                {
                  
                    var parent1 = fitnessList[random.Next(20)].Route;
                    var parent2 = fitnessList[random.Next(20)].Route;

             
                    var child = OrderCrossover(parent1, parent2, random);

                   
                    if (random.NextDouble() < 0.05)
                    {
                        int idx1 = random.Next(child.Count);
                        int idx2 = random.Next(child.Count);
                        (child[idx1], child[idx2]) = (child[idx2], child[idx1]);
                    }

                    newPopulation.Add(child);
                }
                population = newPopulation;
            }

      
            var bestOverall = population.OrderBy(route => CalculateRouteDistance(route, pois, startLat, startLng)).First();
            return bestOverall;
        }

        // Fitnesz segédfüggvény: Kiszámolja a teljes útvonal hosszát (Start -> 1. POI -> 2. POI ...)
        private double CalculateRouteDistance(List<int> route, List<Poi> pois, double startLat, double startLng)
        {
            double dist = 0;
            double currentLat = startLat;
            double currentLng = startLng;

            foreach (var id in route)
            {
                var poi = pois.First(p => p.Id == id);
                dist += HaversineKm(currentLat, currentLng, poi.Latitude, poi.Longitude);
                currentLat = poi.Latitude;
                currentLng = poi.Longitude;
            }
            return dist; 
        }

        private List<int> OrderCrossover(List<int> p1, List<int> p2, Random rand)
        {
            int size = p1.Count;
            int start = rand.Next(size);
            int end = rand.Next(start, size);
            if (start > end) (start, end) = (end, start);

            var child = new int[size];
            for (int i = 0; i < size; i++) child[i] = -1;

          
            for (int i = start; i <= end; i++) child[i] = p1[i];

           
            int currentP2Index = 0;
            for (int i = 0; i < size; i++)
            {
                if (child[i] == -1)
                {
                    while (child.Contains(p2[currentP2Index])) currentP2Index++;
                    child[i] = p2[currentP2Index];
                }
            }
            return child.ToList();
        }

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

        
        private async Task<List<int>> GreedyAiAsync(
            List<Poi> pois,
            double startLat,
            double startLng,
            int userId)
        {
            var prefs = await _preferences.GetPreferencesAsync(userId);

            var remaining = new List<Poi>(pois);
            var route = new List<int>();

            double clat = startLat;
            double clng = startLng;

            while (remaining.Count > 0)
            {
                double bestScore = double.NegativeInfinity;
                Poi? bestPoi = null;

                foreach (var candidate in remaining)
                {
                    double d = HaversineKm(clat, clng, candidate.Latitude, candidate.Longitude);
                    double score = await _ai.ScoreCandidateAsync(candidate.Id, d, prefs);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPoi = candidate;
                    }
                }

                if (bestPoi == null) break;

                route.Add(bestPoi.Id);
                clat = bestPoi.Latitude;
                clng = bestPoi.Longitude;
                remaining.Remove(bestPoi);
            }

            return route;
        }
        private async Task<Itinerary> OptimizeInternal(OptimizeRequest req)
        {
          
            var pois = await _db.Pois
                .Where(p => req.PoiIds.Contains(p.Id))
                .ToListAsync();

            if (!pois.Any()) throw new Exception("Nincsenek érvényes POI-k a tervezéshez.");

          
            double startLat = req.StartLat;
            double startLng = req.StartLng;

            if (req.StartPoiId.HasValue)
            {
                var startPoi = await _db.Pois.FindAsync(req.StartPoiId.Value);
                if (startPoi != null)
                {
                    startLat = startPoi.Latitude;
                    startLng = startPoi.Longitude;
                }
            }

            
            string mode = req.Mode?.ToLowerInvariant() ?? "genetic";
            List<int> route = mode switch
            {
                "genetic" => GeneticAlgorithm(pois, startLat, startLng),
                "ai" => await GreedyAiAsync(pois, startLat, startLng, req.UserId),
                "priority" => OrderByPriority(pois, startLat, startLng),
                "distance" => GreedyByDistance(pois, startLat, startLng),
                _ => GreedyMixed(pois, startLat, startLng, req.WeightDistance ?? 1.0, req.WeightPriority ?? 1.0)
            };

           
            var itinerary = new Itinerary
            {
                UserId = req.UserId,
                StartLat = startLat,
                StartLng = startLng,
                TravelMode = req.TravelMode ?? "walk",
                PoiListJson = JsonSerializer.Serialize(req.PoiIds),
                OptimizedRouteJson = JsonSerializer.Serialize(route)
            };

            _db.Itineraries.Add(itinerary);
            await _db.SaveChangesAsync();

            return itinerary;
        }
    }
}
