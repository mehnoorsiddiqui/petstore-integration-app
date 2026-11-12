using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PetstoreWeb.Services;
using SwaggerPetstoreOpenApi310.Models;
using SwaggerPetstoreOpenApi310.Models.Enums;

namespace PetstoreWeb.Pages;

public class CreateOrderModel(PetstoreOrderService service) : PageModel
{
    [BindProperty]
    [System.ComponentModel.DataAnnotations.Range(1, long.MaxValue, ErrorMessage = "Enter a valid pet id.")]
    public long PetId { get; set; }

    [BindProperty]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "Enter a valid quantity.")]
    public int Quantity { get; set; }

    public Order? CreatedOrder { get; private set; }

    public string? Message { get; private set; }

    public bool OutcomeUnknown { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await service.PlaceOrderAsync(PetId, Quantity, ct);

        CreatedOrder = result.Order;
        Message = result.Error;
        OutcomeUnknown = result.OutcomeUnknown;

        return Page();
    }

    public static string FormatStatus(OrderStatus? status) =>
        status is null
            ? "—"
            : status.Match(
                onPlaced: () => "placed",
                onApproved: () => "approved",
                onDelivered: () => "delivered",
                otherwise: raw => raw);
}
