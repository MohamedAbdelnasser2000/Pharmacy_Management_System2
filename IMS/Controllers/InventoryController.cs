using System;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using IMS.Services;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.WarehouseStaff)]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly INotificationService _notificationService;

        public InventoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // Stock Movements
        public async Task<IActionResult> Movements()
        {
            var userString = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userString ?? "");
            var branchId = user?.BranchId;

            var movementsQuery = _context.StockMovements
                .Include(sm => sm.Product)
                .Include(sm => sm.Warehouse)
                .AsQueryable();

            if (branchId.HasValue && !User.IsInRole(RoleNamesConstants.Admin))
            {
                movementsQuery = movementsQuery.Where(sm => sm.Warehouse.BranchId == branchId);
            }

            var movements = await movementsQuery
                .OrderByDescending(sm => sm.MovementDate)
                .ToListAsync();
            return View(movements);
        }

        public IActionResult AddMovement()
        {
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name");
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMovement(StockMovement movement)
        {
            // Remove validation for navigation properties
            ModelState.Remove("Product");
            ModelState.Remove("Warehouse");

            if (ModelState.IsValid)
            {
                var userString = _userManager.GetUserId(User);
                var user = await _userManager.FindByIdAsync(userString ?? "");
                movement.BranchId = user?.BranchId;

                movement.MovementDate = AppClock.UtcNow;
                movement.ReferenceNumber = movement.ReferenceNumber ?? $"MVT-{AppClock.UtcNow:yyyyMMddHHmmss}";
                movement.Reason = string.IsNullOrWhiteSpace(movement.Reason) ? "تعديل مخزون" : movement.Reason;
                _context.Add(movement);

                // Update inventory
                var inventory = await _context.ProductInventories
                    .FirstOrDefaultAsync(pi => pi.ProductId == movement.ProductId && pi.WarehouseId == movement.WarehouseId);

                if (inventory != null)
                {
                    if (movement.MovementType == "إدخال" || movement.MovementType == "In")
                    {
                        inventory.OnHandQuantity += movement.Quantity;
                    }
                    else if (movement.MovementType == "إخراج" || movement.MovementType == "Out")
                    {
                        inventory.OnHandQuantity -= movement.Quantity;
                    }

                    inventory.LastUpdated = AppClock.UtcNow;

                    if (inventory.OnHandQuantity < 0)
                    {
                        ModelState.AddModelError("", "لا يمكن أن يكون المخزون سالباً؛ تحقق من الكمية المدخلة");
                        ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", movement.ProductId);
                        ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", movement.WarehouseId);
                        return View(movement);
                    }
                }
                else
                {
                    if (movement.MovementType == "إخراج" || movement.MovementType == "Out")
                    {
                        ModelState.AddModelError("", "لا يوجد مخزون متاح لهذا المنتج في المستودع المحدد");
                        ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", movement.ProductId);
                        ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", movement.WarehouseId);
                        return View(movement);
                    }

                    _context.ProductInventories.Add(new ProductInventory
                    {
                        ProductId = movement.ProductId,
                        WarehouseId = movement.WarehouseId,
                        OnHandQuantity = movement.Quantity,
                        ReservedQuantity = 0,
                        ReorderPoint = 0,
                        LastUpdated = AppClock.UtcNow,
                        NextExpiryDate = null
                    });
                }

                await _context.SaveChangesAsync();

                // Build-in Low Stock Check after saving changes
                if (movement.MovementType == "إخراج" || movement.MovementType == "Out")
                {
                    var product = await _context.Products.FindAsync(movement.ProductId);
                    if (product != null && product.LowStockThreshold.HasValue)
                    {
                        var totalStock = await _context.ProductInventories
                            .Where(pi => pi.ProductId == movement.ProductId)
                            .SumAsync(pi => pi.OnHandQuantity);

                        if (totalStock <= product.LowStockThreshold.Value)
                        {
                            await _notificationService.CreateNotificationAsync(
                                "تحذير: نقص مخزون حرج",
                                $"الدواء '{product.Name}' وصل للحد الأدنى للمخزون بعد تعديل يدوي. المتبقي الإجمالي: {totalStock:N2}",
                                "Warning",
                                null,
                                $"/Inventory/Report"
                            );
                        }
                    }
                }

                return RedirectToAction(nameof(Movements));
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", movement.ProductId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", movement.WarehouseId);
            return View(movement);
        }

        // Inventory Report
        public async Task<IActionResult> Report()
        {
            var userString = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userString ?? "");
            var branchId = user?.BranchId;

            var inventoryQuery = _context.ProductInventories
                .Include(pi => pi.Product)
                .Include(pi => pi.Warehouse)
                .AsQueryable();

            if (branchId.HasValue && !User.IsInRole(RoleNamesConstants.Admin))
            {
                inventoryQuery = inventoryQuery.Where(pi => pi.Warehouse.BranchId == branchId);
            }

            var inventory = await inventoryQuery
                .OrderBy(pi => pi.Product.Name)
                .ToListAsync();
            return View(inventory);
        }

        // Stock Transfer between warehouses
        public IActionResult Transfer()
        {
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name");
            ViewData["FromWarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            ViewData["ToWarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(int productId, int fromWarehouseId, int toWarehouseId, decimal quantity, string? notes)
        {
            if (fromWarehouseId == toWarehouseId)
            {
                ModelState.AddModelError("", "لا يمكن التحويل إلى نفس المستودع");
                ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", productId);
                ViewData["FromWarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", fromWarehouseId);
                ViewData["ToWarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", toWarehouseId);
                return View();
            }

            // Deduct from source warehouse
            var fromInventory = await _context.ProductInventories
                .FirstOrDefaultAsync(pi => pi.ProductId == productId && pi.WarehouseId == fromWarehouseId);

            if (fromInventory == null || fromInventory.OnHandQuantity < quantity)
            {
                ModelState.AddModelError("", "الكمية غير متوفرة في المستودع المصدر");
                ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", productId);
                ViewData["FromWarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", fromWarehouseId);
                ViewData["ToWarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", toWarehouseId);
                return View();
            }

            var sourceExpiryDate = fromInventory.NextExpiryDate;

            fromInventory.OnHandQuantity -= quantity;
            fromInventory.LastUpdated = AppClock.UtcNow;
            if (fromInventory.OnHandQuantity <= 0)
            {
                fromInventory.NextExpiryDate = null;
            }

            // Add to destination warehouse
            var toInventory = await _context.ProductInventories
                .FirstOrDefaultAsync(pi => pi.ProductId == productId && pi.WarehouseId == toWarehouseId);

            if (toInventory != null)
            {
                toInventory.OnHandQuantity += quantity;
                toInventory.LastUpdated = AppClock.UtcNow;
                if (sourceExpiryDate.HasValue)
                {
                    if (!toInventory.NextExpiryDate.HasValue || sourceExpiryDate.Value < toInventory.NextExpiryDate.Value)
                    {
                        toInventory.NextExpiryDate = sourceExpiryDate;
                    }
                }
            }
            else
            {
                _context.ProductInventories.Add(new ProductInventory
                {
                    ProductId = productId,
                    WarehouseId = toWarehouseId,
                    OnHandQuantity = quantity,
                    ReservedQuantity = 0,
                    ReorderPoint = 0,
                    LastUpdated = AppClock.UtcNow,
                    NextExpiryDate = sourceExpiryDate
                });
            }

            // Record movements
            var refNumber = $"TRF-{AppClock.UtcNow:yyyyMMddHHmmss}";

            _context.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                WarehouseId = fromWarehouseId,
                MovementType = "إخراج",
                Quantity = quantity,
                MovementDate = AppClock.UtcNow,
                Reason = "تحويل بين المستودعات",
                ReferenceNumber = refNumber
            });

            _context.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                WarehouseId = toWarehouseId,
                MovementType = "إدخال",
                Quantity = quantity,
                MovementDate = AppClock.UtcNow,
                Reason = "تحويل بين المستودعات",
                ReferenceNumber = refNumber
            });

            await _context.SaveChangesAsync();

            // Low Stock Check for the source warehouse/product
            var productRef = await _context.Products.FindAsync(productId);
            if (productRef != null && productRef.LowStockThreshold.HasValue)
            {
                var totalStock = await _context.ProductInventories
                    .Where(pi => pi.ProductId == productId)
                    .SumAsync(pi => pi.OnHandQuantity);

                if (totalStock <= productRef.LowStockThreshold.Value)
                {
                    await _notificationService.CreateNotificationAsync(
                        "تنبيه: مخزون منخفض بعد التحويل",
                        $"بعد عملية التحويل، وصل رصيد '{productRef.Name}' الإجمالي إلى {totalStock:N2} (الحد الأدنى: {productRef.LowStockThreshold})",
                        "Warning",
                        null,
                        $"/Inventory/Report"
                    );
                }
            }

            return RedirectToAction(nameof(Movements));
        }

        // Low Stock Alert
        public async Task<IActionResult> LowStock()
        {
            var userString = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userString ?? "");
            var branchId = user?.BranchId;

            var productsQuery = _context.Products
                .Include(p => p.Inventories)
                .ThenInclude(i => i.Warehouse)
                .AsQueryable();

            var products = await productsQuery.ToListAsync();

            var lowStockProducts = products
                .Where(p => {
                    var branchStock = p.Inventories
                        .Where(i => !branchId.HasValue || i.Warehouse.BranchId == branchId)
                        .Sum(i => i.OnHandQuantity);
                    
                    return p.LowStockThreshold.HasValue && p.LowStockThreshold.Value > 0 && branchStock <= p.LowStockThreshold.Value;
                })
                .OrderBy(p => p.TotalQuantity)
                .ToList();

            return View(lowStockProducts);
        }
    }
}
