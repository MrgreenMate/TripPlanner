namespace TripPlanner.Api.Models
{
    public class Review
    {
        public int Id { get; set; }    // Review_ID
        public int PoiId { get; set; }
        public Poi? Poi { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int Score { get; set; } // 1-5
        public string? Comment { get; set; }
    }
}
