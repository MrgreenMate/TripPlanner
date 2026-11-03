namespace TripPlanner.Api.DTOs
{
    public class CreateReviewDto
    {
        public int Rating { get; set; }   
        public string? Comment { get; set; }
        public int? UserId { get; set; }
    }

    public class ReviewDto
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public int? UserId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
