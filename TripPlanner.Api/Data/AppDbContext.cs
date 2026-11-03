using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

        public DbSet<Poi> Pois { get; set; } = null!;
        public DbSet<Itinerary> Itineraries { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
     
        }
    }
}
