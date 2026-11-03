namespace TripPlanner.Api.Models
{
    public class Poi
    {
        public int Id { get; set; }       
        public string Name { get; set; } = null!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; } = 0;
    }
}
