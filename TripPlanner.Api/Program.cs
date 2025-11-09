using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// **Fix HTTP port**
builder.WebHost.UseUrls("http://localhost:5130");

// EF + SQLite
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=tripplanner_dev.db"));

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("dev", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// pipeline

var app = builder.Build();
app.UseCors("dev");

// Swagger mindig engedélyezve fejlesztéshez
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TripPlanner API v1");
    c.RoutePrefix = "swagger";
});

// ❗ Kapcsold ki a HTTPS redirectet fejlesztés alatt:
//// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

// DB init
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}


app.Run();
