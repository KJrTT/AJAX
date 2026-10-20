using Microsoft.AspNetCore.Mvc;
using WebApplication3.Application.Common.Abstractions;
using WebApplication3.Application.Orders.CreateOrder;
using WebApplication3.Application.Orders.GetOrder; // Для GetOrderUseCase
using WebApplication3.Web.Extensions;
using WebApplication3.Web.Models;

namespace WebApplication3.Web.Controllers
{
    public class OrdersController : Controller
    {
        private readonly CreateOrderUseCase _createOrder;
        private readonly IProductRepository _products;
        private readonly GetOrderUseCase _getOrder; // Добавляем UseCase для получения заказа

        public OrdersController(
            CreateOrderUseCase createOrder,
            IProductRepository products,
            GetOrderUseCase getOrder) // Внедряем через DI
        {
            _createOrder = createOrder;
            _products = products;
            _getOrder = getOrder;
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var products = await _products.GetAllAsync(ct);
            ViewBag.Products = products;

            var model = new CreateOrderViewModel
            {
                Items = new List<CreateOrderItemViewModel> { new() }
            };
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                var products = await _products.GetAllAsync(ct);
                ViewBag.Products = products;
                return View(vm);
            }

            var command = new CreateOrderCommand(
                vm.CustomerEmail,
                vm.Items.Select(i => new CreateOrderItem(i.ProductId, i.Quantity)).ToList());

            var result = await _createOrder.ExecuteAsync(command, ct);

            return result.ToActionResult(this, id => RedirectToAction(nameof(Details), new { id }));
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken ct)
        {
            // Получаем заказ из базы
            var result = await _getOrder.ExecuteAsync(new GetOrderQuery(id), ct);

            // Если заказ не найден — 404
            if (!result.IsSuccess)
                return NotFound();

            // Преобразуем OrderDto в OrderDetailsViewModel для View
            var dto = result.Value!;
            var model = new OrderDetailsViewModel
            {
                Id = dto.Id,
                CustomerEmail = dto.CustomerEmail,
                Total = dto.Total,
                Status = "Создан", // Можно брать реальный статус, если он есть в DTO
                CanBeCancelled = true, // Логику отмены можно добавить позже
                Items = dto.Items.Select(i => new OrderItemViewModel
                {
                    ProductId = i.ProductId,
                    Name = i.Name,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList()
            };

            return View(model); // Передаем модель во View
        }
    }
}
