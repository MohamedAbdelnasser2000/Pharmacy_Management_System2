using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Areas.ECommerce.Controllers
{
    [Area("ECommerce")]
    [Authorize]
    public class ECommerceAccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ECommerceAccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var orders = await _context.ECommerceOrders
                .Where(o => o.CustomerEmail == user.Email)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            var totalOrders = await _context.ECommerceOrders
                .CountAsync(o => o.CustomerEmail == user.Email);

            var totalSpent = await _context.ECommerceOrders
                .Where(o => o.CustomerEmail == user.Email && o.Status == ECommerceOrderStatus.Delivered)
                .SumAsync(o => o.GrandTotal);

            ViewBag.User = user;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalSpent = totalSpent;

            return View(orders);
        }

        public async Task<IActionResult> Orders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var orders = await _context.ECommerceOrders
                .Where(o => o.CustomerEmail == user.Email)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }
    }
}
