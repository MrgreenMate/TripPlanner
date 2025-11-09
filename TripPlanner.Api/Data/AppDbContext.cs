using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Poi> Pois => Set<Poi>();
        public DbSet<Itinerary> Itineraries => Set<Itinerary>();
        public DbSet<Review> Reviews => Set<Review>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Egyedi konfigurációk ha kell
        }
    }
}
