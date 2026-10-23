using Client;
using Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var api = builder.Configuration["Api:BaseUrl"];
var baseAddress = string.IsNullOrWhiteSpace(api) ? builder.HostEnvironment.BaseAddress : api;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(baseAddress) });
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OceanService>();

var host = builder.Build();

await host.RunAsync();
