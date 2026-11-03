using System.Text.Json;

namespace TripPlanner.Api.Services
{
    public class OverpassService
    {
        private readonly HttpClient _httpClient;

        public OverpassService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TripPlannerApp/1.0");
        }

        public async Task<List<RawPoi>> GetNearbyPoisAsync(double lat, double lng, int radiusMeters = 3000)
        {
          
            string query = $@"[out:json][timeout:15];
            (
              node[""tourism""~""museum|attraction|viewpoint|gallery|artwork""](around:{radiusMeters},{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)});
              node[""historic""~""monument|memorial|castle|ruins|archaeological_site""](around:{radiusMeters},{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)});
            );
            out body;";

            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("data", query)
    });

            try
            {
               
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

             
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);

                var rawPois = new List<RawPoi>();
                var elements = doc.RootElement.GetProperty("elements").EnumerateArray();

                foreach (var el in elements)
                {
                    if (el.TryGetProperty("tags", out var tags) && tags.TryGetProperty("name", out var nameProp))
                    {
                        rawPois.Add(new RawPoi
                        {
                            Name = nameProp.GetString()!,
                            Latitude = el.GetProperty("lat").GetDouble(),
                            Longitude = el.GetProperty("lon").GetDouble()
                        });
                    }
                }

                return rawPois.DistinctBy(p => p.Name).Take(20).ToList();
            }
            catch (HttpRequestException ex)
            {
                
                Console.WriteLine($"[Overpass API Hiba]: {ex.Message}");
                return new List<RawPoi>();
            }
            catch (TaskCanceledException ex)
            {
               
                Console.WriteLine($"[Overpass API Időtúllépés]: {ex.Message}");
                return new List<RawPoi>();
            }
            catch (Exception ex)
            {
                
                Console.WriteLine($"[Váratlan hiba az Overpass híváskor]: {ex.Message}");
                return new List<RawPoi>();
            }
        }
    }

    // Segédosztály a nyers adatoknak
    public class RawPoi
    {
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}