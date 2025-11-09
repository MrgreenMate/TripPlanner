using System.Text.Json;

namespace TripPlanner.Api.Models
{
    public class Itinerary
    {
        public int Id { get; set; }            // Itinerary_ID
        public int UserId { get; set; }

        public string PoiListJson { get; set; } = JsonSerializer.Serialize(new int[] { });
        public string? OptimizedRouteJson { get; set; }
    }
}
