using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Areas.ECommerce.Controllers
{
    [Area("ECommerce")]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IECommerceCartService _cartService;

        public CartController(ApplicationDbContext context, IECommerceCartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        public IActionResult Index()
        {
            var cart = _cartService.GetCart(HttpContext.Session);
            ViewBag.CartTotal = _cartService.GetCartTotal(HttpContext.Session);
            ViewBag.CartCount = _cartService.GetCartCount(HttpContext.Session);
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var product = await _context.Products
                .Include(p => p.Inventories)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return Json(new { success = false, message = "المنتج غير موجود" });

            var availableQty = product.Inventories?.Sum(i => i.OnHandQuantity) ?? 0;
            if (availableQty <= 0)
                return Json(new { success = false, message = "المنتج غير متوفر حالياً" });

            var cart = _cartService.GetCart(HttpContext.Session);
            var existingItem = cart.FirstOrDefault(c => c.ProductId == productId);
            var currentQtyInCart = existingItem?.Quantity ?? 0;

            if (currentQtyInCart + quantity > availableQty)
                return Json(new { success = false, message = $"الكمية المتاحة هي {availableQty} فقط" });

            var cartItem = new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.SalePrice ?? product.Price ?? 0,
                Quantity = quantity
            };

            _cartService.AddToCart(HttpContext.Session, cartItem);

            return Json(new
            {
                success = true,
                message = "تمت الإضافة إلى السلة بنجاح",
                cartCount = _cartService.GetCartCount(HttpContext.Session),
                cartTotal = _cartService.GetCartTotal(HttpContext.Session)
            });
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            _cartService.UpdateQuantity(HttpContext.Session, productId, quantity);
            var cart = _cartService.GetCart(HttpContext.Session);
            var item = cart.FirstOrDefault(c => c.ProductId == productId);

            return Json(new
            {
                success = true,
                itemTotal = item?.LineTotal ?? 0,
                cartCount = _cartService.GetCartCount(HttpContext.Session),
                cartTotal = _cartService.GetCartTotal(HttpContext.Session)
            });
        }

        [HttpPost]
        public IActionResult RemoveItem(int productId)
        {
            _cartService.RemoveItem(HttpContext.Session, productId);

            return Json(new
            {
                success = true,
                cartCount = _cartService.GetCartCount(HttpContext.Session),
                cartTotal = _cartService.GetCartTotal(HttpContext.Session)
            });
        }

        [HttpGet]
        public IActionResult GetCartCount()
        {
            return Json(new { count = _cartService.GetCartCount(HttpContext.Session) });
        }
    }
}
