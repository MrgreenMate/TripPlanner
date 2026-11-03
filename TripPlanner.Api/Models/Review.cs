namespace TripPlanner.Api.Models
{
    public class Review
    {
        public int Id { get; set; }

        public int PoiId { get; set; }
        public Poi? Poi { get; set; }

        public int? UserId { get; set; } 

        public int Rating { get; set; } 
        public string? Comment { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
