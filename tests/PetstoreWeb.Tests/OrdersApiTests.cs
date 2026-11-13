using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PetstoreWeb.Tests;

public class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string OrderJson =
        """{"id":7,"petId":3,"quantity":4,"status":"approved","shipDate":"2026-11-12T04:02:15.129+00:00","complete":true}""";

    private readonly WebApplicationFactory<Program> _factory;

    public OrdersApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(Options.DefaultName)
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        }).CreateClient();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    [Fact]
    public async Task Post_ValidBody_ReturnsOrderAsJson()
    {
        using var client = CreateClient(request =>
            request.Method == HttpMethod.Post
                ? Json(HttpStatusCode.OK, OrderJson)
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        using var response = await client.PostAsJsonAsync("/api/orders", new { petId = 3, quantity = 4 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/orders/7", response.Headers.Location?.OriginalString);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(7, json.GetProperty("orderId").GetInt64());
        Assert.Equal(3, json.GetProperty("petId").GetInt64());
        Assert.Equal(4, json.GetProperty("quantity").GetInt32());
        Assert.Equal("approved", json.GetProperty("status").GetString());
        Assert.True(json.GetProperty("complete").GetBoolean());
        Assert.True(json.TryGetProperty("shipDate", out var shipDate));
        Assert.NotNull(shipDate.GetString());
    }

    [Fact]
    public async Task Post_SendsOrderToPetstoreAsJson()
    {
        string? contentType = null;
        string? sentBody = null;
        using var client = CreateClient(request =>
        {
            contentType = request.Content?.Headers.ContentType?.MediaType;
            sentBody = request.Content?.ReadAsStringAsync().Result;
            return Json(HttpStatusCode.OK, OrderJson);
        });

        using var response = await client.PostAsJsonAsync("/api/orders", new { petId = 3, quantity = 4 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", contentType);
        var sent = JsonSerializer.Deserialize<JsonElement>(sentBody!);
        Assert.Equal(3, sent.GetProperty("petId").GetInt64());
        Assert.Equal(4, sent.GetProperty("quantity").GetInt64());
    }

    [Fact]
    public async Task Post_InvalidBody_Returns400WithJsonError()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK, OrderJson));

        using var response = await client.PostAsJsonAsync("/api/orders", new { petId = 0, quantity = 4 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Contains("positive", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_ProviderRejects_ReturnsProviderStatus()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.UnprocessableEntity, """{"message":"bad pet"}"""));

        using var response = await client.PostAsJsonAsync("/api/orders", new { petId = 3, quantity = 4 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("error", out _));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"petId":"abc","quantity":4}""")]
    [InlineData("""{"quantity":4}""")]
    public async Task Post_MalformedBody_Returns400WithJsonError(string body)
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK, OrderJson));

        using var response = await client.PostAsync(
            "/api/orders", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Post_ProviderAuthFailure_Returns502NotProviderStatus()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.Unauthorized, """{"message":"no key"}"""));

        using var response = await client.PostAsJsonAsync("/api/orders", new { petId = 3, quantity = 4 });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Post_UnreadableSuccessBody_Returns502()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK, "not-json"));

        using var response = await client.PostAsJsonAsync("/api/orders", new { petId = 3, quantity = 4 });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Contains("may have been placed", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Get_ProviderInvalidId_Returns404()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid ID supplied", Encoding.UTF8, "text/plain"),
        });

        using var response = await client.GetAsync("/api/orders/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ExistingOrder_ReturnsOrderAsJson()
    {
        using var client = CreateClient(request =>
            request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/7")
                ? Json(HttpStatusCode.OK, OrderJson)
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        using var response = await client.GetAsync("/api/orders/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(7, json.GetProperty("orderId").GetInt64());
        Assert.Equal(3, json.GetProperty("petId").GetInt64());
        Assert.Equal(4, json.GetProperty("quantity").GetInt32());
        Assert.Equal("approved", json.GetProperty("status").GetString());
        Assert.True(json.TryGetProperty("shipDate", out _));
        Assert.True(json.GetProperty("complete").GetBoolean());
    }

    [Fact]
    public async Task Get_MissingOrder_Returns404WithJsonError()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Order not found", Encoding.UTF8, "text/plain"),
        });

        using var response = await client.GetAsync("/api/orders/11");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(json.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Get_NonNumericOrderId_Returns400()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK, OrderJson));

        using var response = await client.GetAsync("/api/orders/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
