using MacroRegimeFactorMonitor.Components;
using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<MacroRegimeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MacroRegime") ?? "Data Source=macro-regime.db"));
builder.Services.AddScoped<FactorScoringService>();
builder.Services.AddScoped<JournalService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MacroRegimeDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await DatabaseSchema.UpgradeAsync(db);
    await DatabaseSeeder.SeedAsync(db);
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
