using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PetstoreWeb.Services;
using SwaggerPetstoreOpenApi310;

namespace PetstoreWeb.Tests;

public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        var response = _responder(request);
        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}

public static class TestClient
{
    public static SwaggerPetstoreOpenApi310Client Returning(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
        return new SwaggerPetstoreOpenApi310Client(
            new HttpClient(handler),
            new SwaggerPetstoreOpenApi310ClientOptions());
    }

    public static SwaggerPetstoreOpenApi310Client ClientWith(StubHandler handler) =>
        new(new HttpClient(handler), new SwaggerPetstoreOpenApi310ClientOptions());

    public static PetstoreOrderService Service(this SwaggerPetstoreOpenApi310Client client) =>
        new(client, NullLogger<PetstoreOrderService>.Instance);
}

public class PetstoreOrderServiceTests
{
    private const string OrderJson =
        """{"id":7,"petId":3,"quantity":4,"status":"approved","shipDate":"2026-11-12T04:02:15.129+00:00","complete":true}""";

    [Fact]
    public async Task PlaceOrderAsync_Success_ReturnsDeserializedOrder()
    {
        var service = TestClient.Returning(HttpStatusCode.OK, OrderJson).Service();

        var result = await service.PlaceOrderAsync(petId: 3, quantity: 4, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.False(result.OutcomeUnknown);
        Assert.NotNull(result.Order);
        Assert.Equal(7, result.Order!.Id);
        Assert.Equal(3, result.Order.PetId);
        Assert.Equal(4, result.Order.Quantity);
    }

    [Fact]
    public async Task PlaceOrderAsync_Rejected_ReturnsFriendlyError()
    {
        var service = TestClient.Returning(HttpStatusCode.UnprocessableEntity, """{"message":"bad pet"}""").Service();

        var result = await service.PlaceOrderAsync(petId: 3, quantity: 4, CancellationToken.None);

        Assert.Null(result.Order);
        Assert.False(result.OutcomeUnknown);
        Assert.Contains("rejected", result.Error);
    }

    [Fact]
    public async Task PlaceOrderAsync_ServerError_ReturnsUnexpectedError()
    {
        var service = TestClient.Returning(HttpStatusCode.InternalServerError, """{"code":500}""").Service();

        var result = await service.PlaceOrderAsync(petId: 3, quantity: 4, CancellationToken.None);

        Assert.Null(result.Order);
        Assert.False(result.OutcomeUnknown);
        Assert.Contains("500", result.Error);
    }

    [Fact]
    public async Task PlaceOrderAsync_ConnectionFailure_ReportsUnknownOutcome()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection reset"));
        var service = TestClient.ClientWith(handler).Service();

        var result = await service.PlaceOrderAsync(petId: 3, quantity: 4, CancellationToken.None);

        Assert.Null(result.Order);
        Assert.True(result.OutcomeUnknown);
        Assert.Contains("may have been placed", result.Error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetOrderAsync_Success_ReturnsDeserializedOrder()
    {
        var service = TestClient.Returning(HttpStatusCode.OK, OrderJson).Service();

        var result = await service.GetOrderAsync(orderId: 7, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Order);
        Assert.Equal(7, result.Order!.Id);
        Assert.Equal(3, result.Order.PetId);
        Assert.Equal(4, result.Order.Quantity);
    }

    [Fact]
    public async Task GetOrderAsync_NotFound_ReturnsFriendlyMessage()
    {
        var service = TestClient.Returning(HttpStatusCode.NotFound, "Order not found").Service();

        var result = await service.GetOrderAsync(orderId: 11, CancellationToken.None);

        Assert.Null(result.Order);
        Assert.Equal("No order was found for id 11.", result.Error);
    }
}
