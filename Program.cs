using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register services
builder.Services.AddSingleton<BetterNotes.Services.AzureAIService>();
builder.Services.AddSingleton<BetterNotes.Services.FileProcessingService>();
builder.Services.AddSingleton<BetterNotes.Services.BlobStorageService>();

// Register HttpClient
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

// Redirect root path to /upload
app.MapGet("/", async context => await Task.Run(() => context.Response.Redirect("/upload")));

app.Run();