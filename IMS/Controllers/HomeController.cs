using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using IMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Products Statistics
        var totalProducts = await _context.Products.CountAsync();
        var products = await _context.Products
            .Include(p => p.Inventories)
            .ToListAsync();
        
        var lowStockProducts = products
            .Where(p => p.LowStockThreshold.HasValue && p.LowStockThreshold.Value > 0 && p.TotalQuantity <= p.LowStockThreshold.Value)
            .OrderBy(p => p.TotalQuantity)
            .Select(p => new { p.Name, Quantity = (int)p.TotalQuantity })
            .Take(5)
            .ToList();

        var totalInventoryValue = products.Sum(p => p.TotalQuantity * (p.Price ?? 0));

        // Customers Statistics
        var totalCustomers = await _context.Customers.CountAsync();
        var activeCustomers = await _context.Orders
            .Where(o => o.OrderDate >= AppClock.UtcNow.AddMonths(-3))
            .Select(o => o.CustomerId)
            .Distinct()
            .CountAsync();

        // Suppliers Statistics
        var totalSuppliers = await _context.Suppliers.CountAsync();
        var activeSuppliers = await _context.PurchaseOrders
            .Where(po => po.OrderDate >= AppClock.UtcNow.AddMonths(-3))
            .Select(po => po.SupplierId)
            .Distinct()
            .CountAsync();

        // Orders Statistics
        var totalOrders = await _context.Orders.CountAsync();
        var todayOrders = await _context.Orders
            .Where(o => o.OrderDate.Date == AppClock.UtcNow.Date)
            .CountAsync();
        var pendingOrders = await _context.Orders
            .Where(o => o.Status == "جديد" || o.Status == "قيد المعالجة")
            .CountAsync();

        // Purchase Orders Statistics
        var totalPurchaseOrders = await _context.PurchaseOrders.CountAsync();
        var pendingPurchaseOrders = await _context.PurchaseOrders
            .Where(po => po.Status == "جديد" || po.Status == "قيد المعالجة")
            .CountAsync();

        // Financial Statistics
        var totalSales = await _context.Orders
            .SumAsync(o => o.TotalAmount);
        
        var totalPurchases = await _context.PurchaseOrders
            .SumAsync(po => po.TotalAmount);

        var todaySales = await _context.Orders
            .Where(o => o.OrderDate.Date == AppClock.UtcNow.Date)
            .SumAsync(o => o.TotalAmount);

        var monthSales = await _context.Orders
            .Where(o => o.OrderDate.Month == AppClock.UtcNow.Month && 
                       o.OrderDate.Year == AppClock.UtcNow.Year)
            .SumAsync(o => o.TotalAmount);

        // Top Selling Products
        var topProducts = await _context.OrderItems
            .Include(oi => oi.Product)
            .GroupBy(oi => new { oi.ProductId, oi.Product.Name })
            .Select(g => new { 
                ProductName = g.Key.Name, 
                TotalQuantity = g.Sum(oi => oi.Quantity),
                TotalRevenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
            })
            .OrderByDescending(x => x.TotalQuantity)
            .Take(5)
            .ToListAsync();

        // Recent Orders
        var recentOrders = await _context.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .Select(o => new {
                o.Id,
                CustomerName = o.Customer.Name,
                o.OrderDate,
                o.TotalAmount,
                o.Status
            })
            .ToListAsync();

        // Pending Payments
        var pendingPayments = await _context.Orders
            .Where(o => o.TotalAmount > o.PaidAmount)
            .SumAsync(o => o.TotalAmount - o.PaidAmount);

        var supplierPendingPayments = await _context.PurchaseOrders
            .Where(po => po.TotalAmount > po.PaidAmount)
            .SumAsync(po => po.TotalAmount - po.PaidAmount);

        var viewModel = new DashboardViewModel
        {
            SummaryCards =
            [
                new DashboardCard
                {
                    Title = "إجمالي المنتجات",
                    Value = totalProducts.ToString(),
                    Icon = "fas fa-boxes",
                    ColorClass = "card-primary text-white"
                },
                new DashboardCard
                {
                    Title = "إجمالي العملاء",
                    Value = totalCustomers.ToString(),
                    Icon = "fas fa-users",
                    ColorClass = "card-primary text-white"
                },
                new DashboardCard
                {
                    Title = "إجمالي الموردين",
                    Value = totalSuppliers.ToString(),
                    Icon = "fas fa-truck",
                    ColorClass = "card-primary text-white"
                },
                new DashboardCard
                {
                    Title = "طلبات اليوم",
                    Value = todayOrders.ToString(),
                    Icon = "fas fa-shopping-cart",
                    ColorClass = "card-primary text-white"
                },
                new DashboardCard
                {
                    Title = "قيمة المخزون",
                    Value = totalInventoryValue.ToString("N0") + " جنيه",
                    Icon = "fas fa-warehouse",
                    ColorClass = "card-primary text-white"
                },
                new DashboardCard
                {
                    Title = "مبيعات اليوم",
                    Value = todaySales.ToString("N0") + " جنيه",
                    Icon = "fas fa-dollar-sign",
                    ColorClass = "card-primary text-white"
                },
                new DashboardCard
                {
                    Title = "مبيعات الشهر",
                    Value = monthSales.ToString("N0") + " جنيه",
                    Icon = "fas fa-chart-line",
                    ColorClass = "card-primary text-white"
                },
                new DashboardCard
                {
                    Title = "منتجات تحت الحد",
                    Value = lowStockProducts.Count.ToString(),
                    Icon = "fas fa-exclamation-triangle",
                    ColorClass = "card-primary text-white"
                }
            ],
            LowStockProducts = lowStockProducts
                .Select(p => (p.Name, p.Quantity))
                .ToList(),
            TopProducts = topProducts
                .Select(p => (p.ProductName, (int)p.TotalQuantity, p.TotalRevenue))
                .ToList(),
            RecentOrders = recentOrders
                .Select(o => (o.Id, o.CustomerName, o.OrderDate, o.TotalAmount, o.Status))
                .ToList(),
            TotalOrders = totalOrders,
            PendingOrders = pendingOrders,
            TotalPurchaseOrders = totalPurchaseOrders,
            PendingPurchaseOrders = pendingPurchaseOrders,
            ActiveCustomers = activeCustomers,
            ActiveSuppliers = activeSuppliers,
            TotalSales = totalSales,
            TotalPurchases = totalPurchases,
            PendingPayments = pendingPayments,
            SupplierPendingPayments = supplierPendingPayments
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
