using WebApplication3.Api.Contracts;
using WebApplication3.Application.Orders.CancleOrder;
using WebApplication3.Application.Orders.CreateOrder;
using WebApplication3.Application.Orders.GetOrder;
using Microsoft.AspNetCore.Mvc;
using WebApplication3.Api.Extensions;

namespace WebApplication3.Api.Controllers
{




    [ApiController]
    [Route("api/orders")]
    public class OrdersApiController : ControllerBase
    {
        private readonly CreateOrderUseCase _createOrder;
        private readonly GetOrderUseCase _getOrder;
        private readonly CancelOrderUseCase _cancelOrder;

        public OrdersApiController(
            CreateOrderUseCase createOrder,
            GetOrderUseCase getOrder,
            CancelOrderUseCase cancelOrder)
        {
            _createOrder = createOrder;
            _getOrder = getOrder;
            _cancelOrder = cancelOrder;
        }

        [HttpPost]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
        {
            // 1. DTO → Command
            var command = new CreateOrderCommand(
                request.CustomerEmail,
                request.Items.Select(i => new CreateOrderItem(i.ProductId, i.Quantity)).ToList());

            // 2. Use Case
            var result = await _createOrder.ExecuteAsync(command, ct);

            // 3. Result → IActionResult (201 Created + Location)
            return result.ToApiResult(this, id =>
                CreatedAtAction(nameof(GetById), new { id }, new { id }));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var result = await _getOrder.ExecuteAsync(new GetOrderQuery(id), ct);

            return result.ToApiResult(this, dto =>
                Ok(new OrderResponse(
                    dto.Id,
                    dto.CustomerEmail,
                    dto.Total,
                    dto.Items.Select(i => new OrderItemResponse(i.ProductId, i.Name, i.Price, i.Quantity)).ToList())));
        }

        [HttpPost("{id:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        {
            var result = await _cancelOrder.ExecuteAsync(new CancelOrderCommand(id), ct);

            return result.ToApiResult(this, _ => NoContent());
        }
    }
}
