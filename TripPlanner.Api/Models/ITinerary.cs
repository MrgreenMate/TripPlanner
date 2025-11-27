namespace TripPlanner.Api.Models
{
    public class Itinerary
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public string? PoiListJson { get; set; }
        public string? OptimizedRouteJson { get; set; }

        public double? StartLat { get; set; }
        public double? StartLng { get; set; }
        public string? TravelMode { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
