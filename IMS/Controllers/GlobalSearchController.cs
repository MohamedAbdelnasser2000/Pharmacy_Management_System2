using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize]
    public class GlobalSearchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public GlobalSearchController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new { items = new List<object>() });

            var user = await _userManager.GetUserAsync(User);
            var branchId = user?.BranchId;
            var isAdmin = User.IsInRole(RoleNamesConstants.Admin);

            query = query.ToLower();

            // 1. Search Products
            var products = await _context.Products
                .Where(p => p.Name.ToLower().Contains(query) || p.Code.ToLower().Contains(query) || p.Barcode.ToLower().Contains(query))
                .Take(5)
                .Select(p => new { 
                    type = "منتج", 
                    id = p.Id, 
                    title = p.Name, 
                    subtitle = p.Code,
                    icon = "fa-pills",
                    link = $"/Products/Details/{p.Id}"
                })
                .ToListAsync();

            // 2. Search Orders (Invoices) - Filter by Branch if not admin
            IQueryable<Order> ordersQuery = _context.Orders;
            if (!isAdmin && branchId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.BranchId == branchId);
            }

            var orders = await ordersQuery
                .Where(o => o.Id.ToString().Contains(query))
                .Take(3)
                .Select(o => new { 
                    type = "فاتورة", 
                    id = o.Id, 
                    title = $"فاتورة رقم #{o.Id}", 
                    subtitle = o.OrderDate.ToString("yyyy/MM/dd"),
                    icon = "fa-file-invoice-dollar",
                    link = $"/Orders/Details/{o.Id}"
                })
                .ToListAsync();

            // 3. Search Customers
            var customers = await _context.Customers
                .Where(c => c.Name.ToLower().Contains(query) || c.PhoneNumber.Contains(query) || (c.NationalId != null && c.NationalId.Contains(query)))
                .Take(5)
                .Select(c => new { 
                    type = "مريض/عميل", 
                    id = c.Id, 
                    title = c.Name, 
                    subtitle = c.PhoneNumber,
                    icon = "fa-user-injured",
                    link = $"/Customers/Details/{c.Id}"
                })
                .ToListAsync();

            // 4. Search Prescriptions - Filter by Branch if not admin
            IQueryable<Prescription> prescriptionsQuery = _context.Prescriptions;
            // Note: Prescription model currently doesn't have BranchId. 
            // If branch-level isolation is needed, consider adding BranchId to Prescription model.

            var prescriptions = await prescriptionsQuery
                .Where(p => p.PrescriptionNumber.Contains(query) || p.PatientName.Contains(query))
                .Take(3)
                .Select(p => new {
                    type = "وصفة طبية",
                    id = p.Id,
                    title = $"وصفة: {p.PrescriptionNumber}",
                    subtitle = p.PatientName,
                    icon = "fa-prescription",
                    link = $"/Prescriptions/Details/{p.Id}"
                })
                .ToListAsync();

            var results = products.Cast<object>()
                .Concat(orders)
                .Concat(customers)
                .Concat(prescriptions);

            return Json(new { items = results });
        }

        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return View(new SearchViewModel());

            var user = await _userManager.GetUserAsync(User);
            var branchId = user?.BranchId;
            var isAdmin = User.IsInRole(RoleNamesConstants.Admin);

            var model = new SearchViewModel { Query = query };

            // Full search results for the index page
            model.Products = await _context.Products
                .Where(p => p.Name.Contains(query) || p.Code.Contains(query) || p.Barcode.Contains(query))
                .Take(20).ToListAsync();

            IQueryable<Order> ordersQuery = _context.Orders.Include(o => o.Customer);
            if (!isAdmin && branchId.HasValue) ordersQuery = ordersQuery.Where(o => o.BranchId == branchId);
            model.Orders = await ordersQuery.Where(o => o.Id.ToString().Contains(query)).Take(20).ToListAsync();

            model.Customers = await _context.Customers
                .Where(c => c.Name.Contains(query) || c.PhoneNumber.Contains(query))
                .Take(20).ToListAsync();

            IQueryable<Prescription> prescriptionsQuery = _context.Prescriptions;
            // if (!isAdmin && branchId.HasValue) prescriptionsQuery = prescriptionsQuery.Where(p => p.BranchId == branchId);
            model.Prescriptions = await prescriptionsQuery.Where(p => p.PrescriptionNumber.Contains(query) || p.PatientName.Contains(query)).Take(20).ToListAsync();

            return View(model);
        }
    }

    public class SearchViewModel
    {
        public string Query { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<Customer> Customers { get; set; } = new();
        public List<Prescription> Prescriptions { get; set; } = new();
    }
}
