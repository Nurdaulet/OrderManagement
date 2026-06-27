using Microsoft.AspNetCore.Mvc;
using OrderManagement.Application.Common.Models;
using OrderManagement.Application.Features.Orders;

namespace OrderManagement.Api.Controllers;

/// <summary>Read access to order documents.</summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController(IOrderDocumentService orderDocumentService) : ControllerBase
{
    /// <summary>Returns all synchronised documents for the specified order.</summary>
    [HttpGet("{orderNumber}/documents")]
    [ProducesResponseType<OrderDocumentsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDocumentsDto>> GetDocuments(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var result = await orderDocumentService.GetDocumentsAsync(orderNumber, cancellationToken);
        return Ok(result);
    }
}
