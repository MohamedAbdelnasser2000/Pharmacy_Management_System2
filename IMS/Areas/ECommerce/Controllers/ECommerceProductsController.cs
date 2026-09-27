using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Areas.ECommerce.Controllers
{
    [Area("ECommerce")]
    [Authorize(Roles = RoleNamesConstants.Admin)]
    public class ECommerceProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ECommerceProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string search, int? categoryId)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            return View(await query.OrderBy(p => p.Name).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> ToggleVisibility(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return Json(new { success = false, message = "المنتج غير موجود" });

            product.ShowInEcommerce = !product.ShowInEcommerce;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isVisible = product.ShowInEcommerce });
        }
    }
}
