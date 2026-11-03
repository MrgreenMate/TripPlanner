using System.Text;
using System.Text.Json;

namespace TripPlanner.Api.Services
{
    public class OpenRouteService
    {
        private readonly HttpClient _httpClient;

        
        private readonly string _apiKey = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6IjMzY2NmNjFmODhlMDQwMDJhOTFlZTVkZmY2NmVhNDdmIiwiaCI6Im11cm11cjY0In0=";

        public OpenRouteService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

       
        public async Task<JsonElement?> GetRealRouteGeometryAsync(List<double[]> coordinates, string travelMode = "walk")
        {
            
            var profile = travelMode == "drive" ? "driving-car" : "foot-walking";
            var url = $"https://api.openrouteservice.org/v2/directions/{profile}/geojson";

           
            var requestBody = new { coordinates = coordinates };
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("Authorization", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[ORS Hiba] {response.StatusCode}");
                    return null;
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);

                
                var geometry = doc.RootElement
                                  .GetProperty("features")[0]
                                  .GetProperty("geometry")
                                  .Clone(); 

                return geometry;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ORS Kivétel] {ex.Message}");
                return null;
            }
        }
    }
}