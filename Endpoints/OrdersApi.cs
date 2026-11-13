using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using PetstoreWeb.Services;
using SwaggerPetstoreOpenApi310.Models;

namespace PetstoreWeb.Endpoints;

public sealed record CreateOrderRequestBody(long? PetId, int? Quantity);

public sealed record OrderDto
{
    [JsonPropertyName("orderId")]
    public long? OrderId { get; init; }

    [JsonPropertyName("petId")]
    public long? PetId { get; init; }

    [JsonPropertyName("quantity")]
    public long? Quantity { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("shipDate")]
    public DateTimeOffset? ShipDate { get; init; }

    [JsonPropertyName("complete")]
    public bool? Complete { get; init; }
}

public static class OrdersApi
{
    public static IEndpointRouteBuilder MapOrderApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders");

        group.MapPost("/", async (
            HttpRequest request,
            PetstoreOrderService service,
            CancellationToken ct) =>
        {
            CreateOrderRequestBody? body;
            try
            {
                body = await request.ReadFromJsonAsync<CreateOrderRequestBody>(ct);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                // Malformed JSON, wrong types, or a non-JSON content type.
                body = null;
            }

            if (body is null || body.PetId is not > 0 || body.Quantity is not > 0)
            {
                return Results.BadRequest(new { error = "Body must be JSON: {\"petId\": <positive number>, \"quantity\": <positive number>}." });
            }

            var result = await service.PlaceOrderAsync(body.PetId.Value, body.Quantity.Value, ct);

            if (result.Order is not null)
            {
                var dto = result.Order.ToDto();
                return dto.OrderId is long id and > 0
                    ? Results.Created($"/api/orders/{id}", dto)
                    : Results.Json(dto, statusCode: StatusCodes.Status201Created);
            }

            return ErrorResult(result);
        })
        .Accepts<CreateOrderRequestBody>("application/json");

        group.MapGet("/{orderId}", async (
            string orderId,
            PetstoreOrderService service,
            CancellationToken ct) =>
        {
            if (!long.TryParse(orderId, out var id) || id <= 0)
            {
                return Results.BadRequest(new { error = "Order id must be a positive number." });
            }

            var result = await service.GetOrderAsync(id, ct);

            if (result.Order is not null)
            {
                return Results.Ok(result.Order.ToDto());
            }

            // Petstore answers 400 for ids it does not recognise; to our callers that is "not found".
            return result.StatusCode == HttpStatusCode.BadRequest
                ? Results.NotFound(new { error = result.Error })
                : ErrorResult(result);
        });

        return app;
    }

    // Provider statuses that describe the caller's input are passed through; anything else
    // (auth/config problems on our side, 5xx, timeouts, unknown outcomes) is a 502 from this API.
    private static readonly HashSet<HttpStatusCode> PassThroughStatuses =
    [
        HttpStatusCode.BadRequest,
        HttpStatusCode.NotFound,
        HttpStatusCode.Conflict,
        HttpStatusCode.UnprocessableEntity,
    ];

    private static IResult ErrorResult(PetstoreOrderResult result)
    {
        var status = !result.OutcomeUnknown
            && result.StatusCode is HttpStatusCode provider
            && PassThroughStatuses.Contains(provider)
                ? (int)provider
                : StatusCodes.Status502BadGateway;

        return Results.Json(
            new { error = result.Error ?? "The request could not be completed." },
            statusCode: status);
    }

    private static OrderDto ToDto(this Order order) => new()
    {
        OrderId = order.Id,
        PetId = order.PetId,
        Quantity = order.Quantity,
        Status = order.Status?.Value,
        ShipDate = order.ShipDate,
        Complete = order.Complete,
    };
}
