using ParkingOCR.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers and JSON serialization options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Register ParkingOCR Core services (ONNX classifier, CV pipeline, Romanian plate validator)
builder.Services.AddParkingOcr();

// CORS for local development and integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// Serve static frontend files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();

// SPA fallback: any non-API route serves index.html
app.MapFallbackToFile("index.html");

app.Run();
