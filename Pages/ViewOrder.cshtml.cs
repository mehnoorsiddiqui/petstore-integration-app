using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PetstoreWeb.Services;
using SwaggerPetstoreOpenApi310.Models;
using SwaggerPetstoreOpenApi310.Models.Enums;

namespace PetstoreWeb.Pages;

public class ViewOrderModel(PetstoreOrderService service) : PageModel
{
    [BindProperty]
    [System.ComponentModel.DataAnnotations.Range(1, long.MaxValue, ErrorMessage = "Enter a valid order id.")]
    public long OrderId { get; set; }

    public Order? Order { get; private set; }

    public string? Message { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await service.GetOrderAsync(OrderId, ct);

        Order = result.Order;
        Message = result.Error;

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
