namespace TripPlanner.Api.DTOs
{
    public class OptimizeRequest
    {
        public int UserId { get; set; }
        public int[] PoiIds { get; set; } = Array.Empty<int>();

       
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public int? StartPoiId { get; set; }

      
        public string Mode { get; set; } = "distance";
        public double? WeightDistance { get; set; }
        public double? WeightPriority { get; set; }

    
        public string TravelMode { get; set; } = "walk";
    }
}
