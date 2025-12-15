var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// If you later host TRC.Api separately, point this to that base URL.
// For now, UI is a shell.
builder.Services.AddHttpClient("trcApi", c =>
{
  c.BaseAddress = new Uri(builder.Configuration["TrcApiBaseUrl"] ?? "http://localhost:5000");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error");
  app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
