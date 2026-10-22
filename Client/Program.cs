using Client;
using Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API is served from the same origin (the gateway forwards /auth, /users
// and /img to the server), so the browser sends the session cookie on its own.
// It can be pointed somewhere else with "Api:BaseUrl" in wwwroot/appsettings.json.
var api = builder.Configuration["Api:BaseUrl"];
var baseAddress = string.IsNullOrWhiteSpace(api) ? builder.HostEnvironment.BaseAddress : api;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(baseAddress) });
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OceanService>();

var host = builder.Build();

await host.RunAsync();
