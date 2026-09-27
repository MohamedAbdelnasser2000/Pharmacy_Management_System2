using System;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Accountant)]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Sales Statistics
            var totalSales = await _context.Orders
                .Where(o => o.Status == "مكتمل")
                .SumAsync(o => o.TotalAmount);

            var totalOrders = await _context.Orders.CountAsync();
            var completedOrders = await _context.Orders.CountAsync(o => o.Status == "مكتمل");

            // Purchase Statistics
            var totalPurchases = await _context.PurchaseOrders
                .SumAsync(po => po.TotalAmount);

            var totalPurchaseOrders = await _context.PurchaseOrders.CountAsync();

            // Profit Calculation
            var profit = totalSales - totalPurchases;

            // Top Selling Products
            var topProducts = await _context.OrderItems
                .GroupBy(oi => oi.Product)
                .Select(g => new { 
                    Product = g.Key.Name, 
                    TotalSold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(10)
                .ToListAsync();

            // Monthly Sales (last 6 months)
            var sixMonthsAgo = AppClock.UtcNow.AddMonths(-6);
            var monthlySalesData = await _context.Orders
                .Where(o => o.OrderDate >= sixMonthsAgo && o.Status == "مكتمل")
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync();

            var monthlySales = monthlySalesData
                .Select(x => new {
                    Month = $"{x.Year}/{x.Month:00}",
                    Total = x.Total
                })
                .OrderBy(x => x.Month)
                .ToList();

            // Inventory Value
            var inventoryValue = await _context.ProductInventories
                .Include(pi => pi.Product)
                .SumAsync(pi => pi.OnHandQuantity * (pi.Product.Price ?? 0));

            // Low Stock Count
            var products = await _context.Products
                .Include(p => p.Inventories)
                .ToListAsync();
            
            var lowStockCount = products
                .Count(p => p.LowStockThreshold.HasValue && p.LowStockThreshold.Value > 0 && p.TotalQuantity <= p.LowStockThreshold.Value);

            // Customer Statistics
            var totalCustomers = await _context.Customers.CountAsync();
            var activeCustomers = await _context.Orders
                .Where(o => o.OrderDate >= AppClock.UtcNow.AddMonths(-3))
                .Select(o => o.CustomerId)
                .Distinct()
                .CountAsync();

            // Supplier Statistics
            var totalSuppliers = await _context.Suppliers.CountAsync();
            var activeSuppliers = await _context.PurchaseOrders
                .Where(po => po.OrderDate >= AppClock.UtcNow.AddMonths(-3))
                .Select(po => po.SupplierId)
                .Distinct()
                .CountAsync();

            ViewBag.TotalSales = totalSales;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.CompletedOrders = completedOrders;
            ViewBag.TotalPurchases = totalPurchases;
            ViewBag.TotalPurchaseOrders = totalPurchaseOrders;
            ViewBag.Profit = profit;
            ViewBag.TopProducts = topProducts;
            ViewBag.MonthlySales = monthlySales;
            ViewBag.InventoryValue = inventoryValue;
            ViewBag.LowStockCount = lowStockCount;
            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.ActiveCustomers = activeCustomers;
            ViewBag.TotalSuppliers = totalSuppliers;
            ViewBag.ActiveSuppliers = activeSuppliers;

            return View();
        }

        public async Task<IActionResult> Sales(DateTime? startDate, DateTime? endDate, string filter = "all")
        {
            var start = startDate ?? AppClock.UtcNow.AddMonths(-1);
            var end = endDate ?? AppClock.UtcNow;

            // Get all orders in the date range for statistics (not filtered)
            var allOrders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();

            // Load orders with customer only
            var salesQuery = _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.OrderDate >= start && o.OrderDate <= end);

            // Apply filter based on payment status
            if (filter == "unpaid")
            {
                // Show orders with remaining balance (not fully paid)
                salesQuery = salesQuery.Where(o => o.PaidAmount < o.TotalAmount);
            }

            var sales = await salesQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Load OrderItems separately
            var orderIds = sales.Select(o => o.Id).ToList();
            var orderItems = await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Product)
                .Where(oi => orderIds.Contains(oi.OrderId))
                .ToListAsync();

            // Assign items to their respective orders
            foreach (var order in sales)
            {
                order.OrderItems = orderItems
                    .Where(oi => oi.OrderId == order.Id)
                    .ToList();
            }

            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            // Use all orders for totals, not just filtered ones
            ViewBag.TotalSales = allOrders.Sum(o => o.TotalAmount);
            ViewBag.TotalPaid = allOrders.Sum(o => o.PaidAmount);
            ViewBag.Filter = filter;

            return View(sales);
        }

        public async Task<IActionResult> Purchases(DateTime? startDate, DateTime? endDate, string filter = "all")
        {
            var start = startDate ?? AppClock.UtcNow.AddMonths(-1);
            var end = endDate ?? AppClock.UtcNow;

            // Get all purchase orders in the date range for statistics (not filtered)
            var allPurchases = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(po => po.OrderDate >= start && po.OrderDate <= end)
                .ToListAsync();

            // Load purchase orders with supplier only
            var purchasesQuery = _context.PurchaseOrders
                .AsNoTracking()
                .Include(po => po.Supplier)
                .Where(po => po.OrderDate >= start && po.OrderDate <= end);

            // Apply filter based on payment status
            if (filter == "unpaid")
            {
                // Show orders with remaining balance (not fully paid)
                purchasesQuery = purchasesQuery.Where(po => po.PaidAmount < po.TotalAmount);
            }

            var purchases = await purchasesQuery
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();

            // Load PurchaseOrderItems separately
            var orderIds = purchases.Select(po => po.Id).ToList();
            var purchaseOrderItems = await _context.PurchaseOrderItems
                .AsNoTracking()
                .Include(poi => poi.Product)
                .Where(poi => orderIds.Contains(poi.PurchaseOrderId))
                .ToListAsync();

            // Assign items to their respective orders
            foreach (var order in purchases)
            {
                order.PurchaseOrderItems = purchaseOrderItems
                    .Where(poi => poi.PurchaseOrderId == order.Id)
                    .ToList();
            }

            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            // Use all purchases for totals, not just filtered ones
            ViewBag.TotalPurchases = allPurchases.Sum(po => po.TotalAmount);
            ViewBag.TotalPaid = allPurchases.Sum(po => po.PaidAmount);
            ViewBag.Filter = filter;

            return View(purchases);
        }

        public async Task<IActionResult> Profit(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? AppClock.UtcNow.AddMonths(-1);
            var end = endDate ?? AppClock.UtcNow;

            var sales = await _context.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= end && o.Status == "مكتمل")
                .SumAsync(o => o.TotalAmount);

            var purchases = await _context.PurchaseOrders
                .Where(po => po.OrderDate >= start && po.OrderDate <= end)
                .SumAsync(po => po.TotalAmount);

            var profit = sales - purchases;

            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            ViewBag.Sales = sales;
            ViewBag.Purchases = purchases;
            ViewBag.Profit = profit;
            ViewBag.ProfitMargin = sales > 0 ? (profit / sales * 100) : 0;

            return View();
        }
        public async Task<IActionResult> InventoryMovement(int? productId, DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? AppClock.UtcNow.AddMonths(-1);
            var end = endDate ?? AppClock.UtcNow;

            var movements = _context.StockMovements
                .Include(sm => sm.Product)
                .Include(sm => sm.Warehouse)
                .Where(sm => sm.MovementDate >= start && sm.MovementDate <= end);

            if (productId.HasValue)
            {
                movements = movements.Where(sm => sm.ProductId == productId);
            }

            ViewBag.Products = new SelectList(_context.Products.OrderBy(p => p.Name), "Id", "Name", productId);
            ViewBag.StartDate = start;
            ViewBag.EndDate = end;

            return View(await movements.OrderByDescending(sm => sm.MovementDate).ToListAsync());
        }

        public async Task<IActionResult> ExpiryReport()
        {
            var alerts = await _context.ExpiryAlerts
                .Include(a => a.Product)
                .OrderBy(a => a.ExpiryDate)
                .ToListAsync();

            return View(alerts);
        }

        public async Task<IActionResult> InactiveItems(int days = 30)
        {
            var cutoffDate = AppClock.UtcNow.AddDays(-days);

            // Get product IDs sold recently
            var soldProductIds = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= cutoffDate)
                .Select(oi => oi.ProductId)
                .Distinct()
                .ToListAsync();

            // Find products not in the sold list that have stock
            var inactiveProducts = await _context.Products
                .Include(p => p.Inventories)
                .Where(p => !soldProductIds.Contains(p.Id) && p.Inventories.Sum(i => i.OnHandQuantity) > 0)
                .Select(p => new InactiveItemViewModel
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    StockQuantity = p.Inventories.Sum(i => i.OnHandQuantity),
                    Price = p.Price ?? 0,
                    CategoryName = p.Category != null ? p.Category.Name : "بدون تصنيف"
                })
                .OrderByDescending(p => p.StockQuantity)
                .ToListAsync();

            ViewBag.Days = days;
            return View(inactiveProducts);
        }

        public async Task<IActionResult> BranchPerformance(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? AppClock.UtcNow.AddMonths(-1);
            var end = endDate ?? AppClock.UtcNow;

            var branchStats = await _context.Branches
                .Select(b => new BranchPerformanceViewModel
                {
                    BranchName = b.Name,
                    TotalSales = _context.Orders
                        .Where(o => o.BranchId == b.Id && o.OrderDate >= start && o.OrderDate <= end && o.Status == "مكتمل")
                        .Sum(o => o.TotalAmount),
                    OrderCount = _context.Orders
                        .Count(o => o.BranchId == b.Id && o.OrderDate >= start && o.OrderDate <= end && o.Status == "مكتمل"),
                    CustomerCount = _context.Orders
                        .Where(o => o.BranchId == b.Id && o.OrderDate >= start && o.OrderDate <= end)
                        .Select(o => o.CustomerId)
                        .Distinct()
                        .Count()
                })
                .OrderByDescending(s => s.TotalSales)
                .ToListAsync();

            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            return View(branchStats);
        }
    }

    public class InactiveItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal StockQuantity { get; set; }
        public decimal Price { get; set; }
        public string CategoryName { get; set; }
        public decimal TotalValue => StockQuantity * Price;
    }

    public class BranchPerformanceViewModel
    {
        public string BranchName { get; set; }
        public decimal TotalSales { get; set; }
        public int OrderCount { get; set; }
        public int CustomerCount { get; set; }
        public decimal AverageOrderValue => OrderCount > 0 ? TotalSales / OrderCount : 0;
    }
}
