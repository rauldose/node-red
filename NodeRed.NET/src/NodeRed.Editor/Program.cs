using NodeRed.Editor.Components;
using NodeRed.Editor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register NodeRed.NET editor services
// Core state management
builder.Services.AddSingleton<EditorState>();
builder.Services.AddSingleton<EditorUIState>();

// Events system (independent)
builder.Services.AddSingleton<Events>();

// Communication
builder.Services.AddScoped<IEditorComms>(sp => EditorCommsFactory.Create("/comms"));

// Editor features
builder.Services.AddSingleton<History>();
builder.Services.AddSingleton<Clipboard>();
builder.Services.AddSingleton<Keyboard>();
builder.Services.AddSingleton<Actions>();

// Node management
builder.Services.AddSingleton<GroupManager>();
builder.Services.AddSingleton<SubflowManager>();
builder.Services.AddSingleton<Library>();

// UI utilities
builder.Services.AddSingleton<Diagnostics>();
builder.Services.AddSingleton<StatusBar>();
builder.Services.AddSingleton<ViewTools>();
builder.Services.AddSingleton<ViewNavigator>();
builder.Services.AddSingleton<Projects>();
builder.Services.AddSingleton<Plugins>();
builder.Services.AddSingleton<Runtime>();
builder.Services.AddSingleton<User>();
builder.Services.AddSingleton<Diff>();

// Note: Bidi, TextFormat, Validators, UiUtils, State are static utility classes,
// not injectable services

builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
