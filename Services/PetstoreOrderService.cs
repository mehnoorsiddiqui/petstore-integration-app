using SwaggerPetstoreOpenApi310;
using SwaggerPetstoreOpenApi310.Core.Exceptions;
using SwaggerPetstoreOpenApi310.Errors;
using SwaggerPetstoreOpenApi310.Models;
using SwaggerPetstoreOpenApi310.Requests.Store;

namespace PetstoreWeb.Services;

public sealed record PetstoreOrderResult
{
    public Order? Order { get; init; }

    public string? Error { get; init; }

    public bool OutcomeUnknown { get; init; }
}

public sealed class PetstoreOrderService(
    SwaggerPetstoreOpenApi310Client client,
    ILogger<PetstoreOrderService> logger)
{
    public async Task<PetstoreOrderResult> PlaceOrderAsync(long petId, int quantity, CancellationToken ct)
    {
        try
        {
            var order = await client.Store.PlaceOrder(
                new PlaceOrderRequest { PetId = petId, Quantity = quantity },
                cancellationToken: ct);

            return new PetstoreOrderResult { Order = order };
        }
        catch (ApiException<PlaceOrderError> ex)
        {
            var body = ex.Error.Match(
                onNoContent: raw => raw.ReadAsString(),
                otherwise: raw => raw.ReadAsString());
            logger.LogError("PlaceOrder rejected with HTTP {StatusCode}: {Body}", (int)ex.StatusCode, body);

            return new PetstoreOrderResult { Error = $"The store rejected the order (HTTP {(int)ex.StatusCode})." };
        }
        catch (ResponseDeserializationException ex)
        {
            logger.LogError(ex, "PlaceOrder returned an unreadable response with HTTP {StatusCode}.", (int)ex.StatusCode);

            return new PetstoreOrderResult { Error = "The store returned a response that could not be processed." };
        }
        catch (SdkTimeoutException ex)
        {
            logger.LogError(ex, "PlaceOrder timed out before a response was received.");

            return new PetstoreOrderResult
            {
                OutcomeUnknown = true,
                Error = "The order may have been placed, but the connection timed out before the result could be confirmed.",
            };
        }
        catch (SdkConnectionException ex)
        {
            logger.LogError(ex, "PlaceOrder could not reach the store.");

            return new PetstoreOrderResult
            {
                OutcomeUnknown = true,
                Error = "The order may have been placed, but the connection to the store failed before the result could be confirmed.",
            };
        }
        catch (AuthSchemeException ex)
        {
            logger.LogError(ex, "Petstore authentication failed for PlaceOrder.");

            return new PetstoreOrderResult { Error = "Petstore authentication is not configured correctly." };
        }
    }

    public async Task<PetstoreOrderResult> GetOrderAsync(long orderId, CancellationToken ct)
    {
        try
        {
            var order = await client.Store.GetOrderById(
                new GetOrderByIdRequest { OrderId = orderId },
                cancellationToken: ct);

            return new PetstoreOrderResult { Order = order };
        }
        catch (ApiException<GetOrderByIdError> ex)
        {
            var body = ex.Error.Match(
                onNoContent: raw => raw.ReadAsString(),
                otherwise: raw => raw.ReadAsString());
            logger.LogError("GetOrderById returned HTTP {StatusCode}: {Body}", (int)ex.StatusCode, body);

            var message = ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest
                ? $"No order was found for id {orderId}."
                : $"The store returned an unexpected error (HTTP {(int)ex.StatusCode}).";

            return new PetstoreOrderResult { Error = message };
        }
        catch (ResponseDeserializationException ex)
        {
            logger.LogError(ex, "GetOrderById returned an unreadable response with HTTP {StatusCode}.", (int)ex.StatusCode);

            return new PetstoreOrderResult { Error = "The store returned a response that could not be processed." };
        }
        catch (SdkTimeoutException ex)
        {
            logger.LogError(ex, "GetOrderById timed out before a response was received.");

            return new PetstoreOrderResult { Error = "The store did not answer in time. Please try again." };
        }
        catch (SdkConnectionException ex)
        {
            logger.LogError(ex, "GetOrderById could not reach the store.");

            return new PetstoreOrderResult { Error = "Could not reach the Petstore. Please try again." };
        }
        catch (AuthSchemeException ex)
        {
            logger.LogError(ex, "Petstore authentication failed for GetOrderById.");

            return new PetstoreOrderResult { Error = "Petstore authentication is not configured correctly." };
        }
    }
}
