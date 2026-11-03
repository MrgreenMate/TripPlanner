namespace TripPlanner.Api.DTOs
{
    public class DiscoverOptimizeRequest
    {
        public int UserId { get; set; }
        public double StartLat { get; set; }
        public double StartLng { get; set; }

        public double RadiusKm { get; set; } = 3.0;
        public int MaxPois { get; set; } = 10;

        public string Mode { get; set; } = "ai";
        public string TravelMode { get; set; } = "walk";
    }
}
