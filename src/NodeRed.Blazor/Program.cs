using NodeRed.Blazor.Components;
using NodeRed.Runtime;
using Syncfusion.Blazor;
using Syncfusion.Licensing;

var builder = WebApplication.CreateBuilder(args);

// Register Syncfusion license
var syncfusionLicense = builder.Configuration["SyncfusionLicense"];
if (!string.IsNullOrEmpty(syncfusionLicense))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicense);
}

// Add Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Add Node-RED runtime services
builder.Services.AddNodeRedRuntime();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Load plugins from the plugins directory
var pluginsPath = Path.Combine(app.Environment.ContentRootPath, "plugins");
app.Services.LoadNodePlugins(pluginsPath);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
