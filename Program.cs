using Microsoft.Extensions.Options;
using PetstoreWeb.Services;
using SwaggerPetstoreOpenApi310;
using SwaggerPetstoreOpenApi310.Core.Configuration;

var builder = WebApplication.CreateBuilder(args);

var petstoreBaseUrl = builder.Configuration["Petstore:BaseUrl"];
if (string.IsNullOrWhiteSpace(petstoreBaseUrl))
{
    throw new InvalidOperationException(
        "Petstore:BaseUrl is not configured. Set it via appsettings.json, user-secrets, or an environment variable before starting the app.");
}

var petstoreApiKey = builder.Configuration["Petstore:ApiKey"];

builder.Services.AddRazorPages();

builder.Services.AddSwaggerPetstoreOpenApi310Client(options =>
{
    options.Server.Default.Production.BaseUrl = petstoreBaseUrl;
    if (!string.IsNullOrWhiteSpace(petstoreApiKey))
    {
        options.ApiKey = petstoreApiKey;
    }

    options.Retry = RetryOptions.Default() with
    {
        Timeout = TimeSpan.FromSeconds(10),
    };
});

builder.Services.AddHttpClient(Options.DefaultName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<PetstoreOrderService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
