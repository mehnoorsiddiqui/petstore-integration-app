using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;

namespace PetstoreWeb.Services;

/// <summary>
/// The generated SDK sends PlaceOrder (POST /store/order) as application/x-www-form-urlencoded,
/// which the Petstore server rejects with HTTP 500 while accepting the same order as JSON.
/// This handler re-encodes that one request body as JSON so the SDK can still be used as-is.
/// </summary>
public sealed class PetstoreOrderJsonBodyHandler : DelegatingHandler
{
    private static readonly HashSet<string> IntegerFields = ["id", "petId", "quantity"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method == HttpMethod.Post
            && request.RequestUri is { } uri
            && uri.AbsolutePath.EndsWith("/store/order", StringComparison.OrdinalIgnoreCase)
            && request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded")
        {
            var form = QueryHelpers.ParseQuery("?" + await request.Content.ReadAsStringAsync(ct));
            var json = new JsonObject();

            foreach (var (key, values) in form)
            {
                var value = values.ToString();
                if (IntegerFields.Contains(key) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    json[key] = number;
                }
                else if (key == "complete" && bool.TryParse(value, out var flag))
                {
                    json[key] = flag;
                }
                else
                {
                    json[key] = value;
                }
            }

            request.Content = new StringContent(json.ToJsonString(), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        return await base.SendAsync(request, ct);
    }
}
