using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;
using TripPlanner.Api.Services;
using System.Text.Json;



var builder = WebApplication.CreateBuilder(args);


// DB
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "tripplanner_dev.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? $"Data Source={dbPath}"));

// Külső API hívásokhoz a HttpClient regisztrálása
builder.Services.AddHttpClient<OverpassService>();
builder.Services.AddHttpClient<AiEnrichmentService>();
builder.Services.AddHttpClient<TripPlanner.Api.Services.OpenRouteService>();

//  CORS 
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("dev", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

//  Controllers + Swagger 

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TripPlanner API", Version = "v1" });
   
});
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddScoped<PreferenceService>();
builder.Services.AddScoped<AiScoringService>();

builder.Services.AddScoped<UserPreferenceService>();
builder.Services.AddScoped<AiRouteService>();



var app = builder.Build();

app.UseCors("dev");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TripPlanner API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthorization();
app.MapControllers();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
   
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = "http://localhost:5130/index.html"; 
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Nem sikerült automatikusan megnyitni a böngészőt: {ex.Message}");
        }
    });
}
app.Run();
