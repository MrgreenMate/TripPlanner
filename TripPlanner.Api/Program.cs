using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.DTOs;
using Swashbuckle.AspNetCore.Filters;

var builder = WebApplication.CreateBuilder(args);

// --- DB (mindig az aktuális mappában lévő fájlt használjuk)
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "tripplanner_dev.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? $"Data Source={dbPath}"));

// --- CORS ---
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("dev", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// --- Controllers + Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TripPlanner API", Version = "v1" });
    c.ExampleFilters(); // ha használod az OptimizeRequestExamples-t
});

// Ha használod az OptimizeRequestExamples-t:
builder.Services.AddSwaggerExamplesFromAssemblyOf<OptimizeRequest>();

var app = builder.Build();

app.UseCors("dev");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TripPlanner API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthorization();
app.MapControllers();

// 🔹 MIGRÁCIÓ FUTTATÁSA INDULÁSKOR
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
