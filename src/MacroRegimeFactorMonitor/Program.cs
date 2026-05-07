using MacroRegimeFactorMonitor.Components;
using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<MacroRegimeDbContext>(options =>
    options.UseConfiguredDatabase(builder.Configuration));
builder.Services.AddScoped<FactorScoringService>();
builder.Services.AddScoped<JournalService>();
builder.Services.AddScoped<StartupSyncService>();

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
    var startupSync = scope.ServiceProvider.GetRequiredService<StartupSyncService>();
    await startupSync.RunAsync();
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
