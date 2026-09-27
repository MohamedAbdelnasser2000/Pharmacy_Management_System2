using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Areas.ECommerce.Controllers
{
    [Area("ECommerce")]
    [Authorize(Roles = RoleNamesConstants.Admin)]
    public class ECommerceOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ECommerceOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status, string? search, int page = 1)
        {
            int pageSize = 20;

            var query = _context.ECommerceOrders
                .Include(o => o.OrderItems)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(o =>
                    o.OrderNumber.Contains(search) ||
                    o.CustomerName.Contains(search) ||
                    o.CustomerPhone.Contains(search));

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Stats
            ViewBag.TotalOrders = await _context.ECommerceOrders.CountAsync();
            ViewBag.PendingOrders = await _context.ECommerceOrders.CountAsync(o => o.Status == ECommerceOrderStatus.Pending);
            ViewBag.ProcessingOrders = await _context.ECommerceOrders.CountAsync(o => o.Status == ECommerceOrderStatus.Processing);
            ViewBag.DeliveredOrders = await _context.ECommerceOrders.CountAsync(o => o.Status == ECommerceOrderStatus.Delivered);
            ViewBag.TotalRevenue = await _context.ECommerceOrders.Where(o => o.Status == ECommerceOrderStatus.Delivered).SumAsync(o => o.GrandTotal);

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.ECommerceOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _context.ECommerceOrders.FindAsync(id);
            if (order == null)
                return NotFound();

            order.Status = status;
            order.UpdatedDate = AppClock.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم تحديث حالة الطلب {order.OrderNumber} إلى {ECommerceOrderStatus.GetArabicName(status)}";
            return RedirectToAction("Details", new { id });
        }
    }
}
