
namespace TripPlanner.Api.Models 
{ 

    public class UserPreferences
    {
        
        public double WeightDistance { get; set; } = 1.0;

       
        public double WeightPriority { get; set; } = 1.0;

      
        public double WeightRating { get; set; } = 1.0;

      
        public int MaxPoiCount { get; set; } = 10;
    }
}
