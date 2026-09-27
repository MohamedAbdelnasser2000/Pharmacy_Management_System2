using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Areas.ECommerce.Controllers
{
    [Area("ECommerce")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IECommerceCartService _cartService;

        public CheckoutController(ApplicationDbContext context, IECommerceCartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        public async Task<IActionResult> Index()
        {
            var cart = _cartService.GetCart(HttpContext.Session);
            if (!cart.Any())
                return RedirectToAction("Index", "Shop");

            var settings = await _context.ECommerceSettings.FirstOrDefaultAsync() ?? new ECommerceSettings();
            var cartTotal = _cartService.GetCartTotal(HttpContext.Session);
            var shippingFee = cartTotal >= settings.FreeShippingThreshold ? 0 : settings.DefaultShippingFee;

            ViewBag.Cart = cart;
            ViewBag.CartTotal = cartTotal;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.GrandTotal = cartTotal + shippingFee;
            ViewBag.FreeShippingThreshold = settings.FreeShippingThreshold;
            ViewBag.CartCount = _cartService.GetCartCount(HttpContext.Session);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(string customerName, string customerEmail,
            string customerPhone, string shippingAddress, string? city, string? region,
            string? postalCode, string? notes, string? paymentMethod)
        {
            var cart = _cartService.GetCart(HttpContext.Session);
            if (!cart.Any())
                return RedirectToAction("Index", "Shop");

            // Validate stock availability for all items
            foreach (var item in cart)
            {
                var product = await _context.Products
                    .Include(p => p.Inventories)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                if (product == null)
                {
                    TempData["Error"] = $"المنتج {item.ProductName} لم يعد متاحاً";
                    return RedirectToAction("Index", "Cart");
                }

                var availableQty = product.Inventories?.Sum(i => i.OnHandQuantity) ?? 0;
                if (item.Quantity > availableQty)
                {
                    TempData["Error"] = $"الكمية المطلوبة من {item.ProductName} تتجاوز المتاح ({availableQty})";
                    return RedirectToAction("Index", "Cart");
                }
            }

            var settings = await _context.ECommerceSettings.FirstOrDefaultAsync() ?? new ECommerceSettings();
            var cartTotal = _cartService.GetCartTotal(HttpContext.Session);
            var shippingFee = cartTotal >= settings.FreeShippingThreshold ? 0 : settings.DefaultShippingFee;

            // Generate order number
            var orderNumber = $"EC-{AppClock.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            var order = new ECommerceOrder
            {
                OrderNumber = orderNumber,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                CustomerPhone = customerPhone,
                ShippingAddress = shippingAddress,
                City = city,
                Region = region,
                PostalCode = postalCode,
                Notes = notes,
                PaymentMethod = paymentMethod ?? "CashOnDelivery",
                TotalAmount = cartTotal,
                ShippingFee = shippingFee,
                GrandTotal = cartTotal + shippingFee,
                Status = ECommerceOrderStatus.Pending,
                OrderDate = AppClock.UtcNow
            };

            foreach (var item in cart)
            {
                order.OrderItems.Add(new ECommerceOrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }

            _context.ECommerceOrders.Add(order);
            await _context.SaveChangesAsync();

            _cartService.ClearCart(HttpContext.Session);

            return RedirectToAction("Confirmation", new { id = order.Id });
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var order = await _context.ECommerceOrders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            return View(order);
        }
    }
}
