using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Areas.ECommerce.Controllers
{
    [Area("ECommerce")]
    public class ShopController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IECommerceCartService _cartService;

        public ShopController(ApplicationDbContext context, IECommerceCartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        public async Task<IActionResult> Index(string? search, int? categoryId, string? sortBy, int page = 1)
        {
            var settings = await _context.ECommerceSettings.FirstOrDefaultAsync();
            if (settings != null && !settings.IsStoreActive)
                return View("StoreClosed");

            int pageSize = 12;

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Inventories)
                .Where(p => p.ShowInEcommerce && p.Inventories.Any(i => i.OnHandQuantity > 0))
                .AsQueryable();

            // Filter prescription products based on settings
            if (settings == null || !settings.ShowPrescriptionProducts)
                query = query.Where(p => !p.RequiresPrescription);

            // Filter by expiry - exclude expired products
            query = query.Where(p => !p.ExpiryDate.HasValue || p.ExpiryDate > AppClock.UtcNow);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    (p.ScientificName != null && p.ScientificName.Contains(search)) ||
                    (p.TradeName != null && p.TradeName.Contains(search)) ||
                    (p.ActiveIngredient != null && p.ActiveIngredient.Contains(search)));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            query = sortBy switch
            {
                "price_asc" => query.OrderBy(p => p.SalePrice ?? p.Price),
                "price_desc" => query.OrderByDescending(p => p.SalePrice ?? p.Price),
                "name" => query.OrderBy(p => p.Name),
                "newest" => query.OrderByDescending(p => p.Id),
                _ => query.OrderBy(p => p.Name)
            };

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCategory = categoryId;
            ViewBag.CurrentSort = sortBy;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.CartCount = _cartService.GetCartCount(HttpContext.Session);
            ViewBag.Settings = settings ?? new ECommerceSettings();

            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Inventories)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            var availableQty = product.Inventories?.Sum(i => i.OnHandQuantity) ?? 0;
            ViewBag.AvailableQuantity = availableQty;
            ViewBag.CartCount = _cartService.GetCartCount(HttpContext.Session);

            // Related products from same category
            var relatedProducts = await _context.Products
                .Include(p => p.Inventories)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id
                            && p.ShowInEcommerce
                            && p.Inventories.Any(i => i.OnHandQuantity > 0)
                            && (!p.ExpiryDate.HasValue || p.ExpiryDate > AppClock.UtcNow))
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            var settings = await _context.ECommerceSettings.FirstOrDefaultAsync();
            ViewBag.Settings = settings ?? new ECommerceSettings();

            return View(product);
        }
    }
}
