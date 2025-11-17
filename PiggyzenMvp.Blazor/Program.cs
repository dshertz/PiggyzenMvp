using System.Globalization;
using System.Net.Http.Headers;
using PiggyzenMvp.Blazor.Components;

var builder = WebApplication.CreateBuilder(args);

// Lägg till CORS-policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("https://localhost:5001", "https://localhost:5002") // Lägg till dina Blazor-URL:er
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Sätt default culture till svenska
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("sv-SE");

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder
    .Services.AddServerSideBlazor()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Services.AddHttpClient(
    "ApiClient",
    client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
    }
);
builder.Services.AddScoped<PiggyzenMvp.Blazor.Services.TransactionFilterState>();

var app = builder.Build();

// Aktivera CORS
app.UseCors();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
