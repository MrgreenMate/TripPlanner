namespace TripPlanner.Api.DTOs
{
    public class OptimizeRequest
    {
        public int UserId { get; set; }
        public int[] PoiIds { get; set; } = Array.Empty<int>();

        // Start megadása EGYIK módon:
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public int? StartPoiId { get; set; } // <-- ÚJ: ha ez meg van adva, innen vesszük a start koordinátát

        /// "distance" | "priority" | "mixed"
        public string Mode { get; set; } = "distance";
        public double? WeightDistance { get; set; }
        public double? WeightPriority { get; set; }

        /// "walk" | "drive" (időbecsléshez). Alap: "walk".
        public string TravelMode { get; set; } = "walk";
    }
}
