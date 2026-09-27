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
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Accountant + "," + RoleNamesConstants.WarehouseStaff)]
    public class PurchaseOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchaseOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchTerm)
        {
            var ordersQuery = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                ordersQuery = ordersQuery.Where(po => 
                    po.Id.ToString().Contains(searchTerm) ||
                    po.Supplier.Name.Contains(searchTerm) ||
                    po.Supplier.PhoneNumber.Contains(searchTerm) ||
                    po.Status.Contains(searchTerm)
                );
            }

            var orders = await ordersQuery
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();
            
            ViewBag.SearchTerm = searchTerm;
            return View(orders);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            // Load order with supplier only
            var order = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(po => po.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            // Load PurchaseOrderItems separately to avoid Cartesian explosion
            var purchaseOrderItems = await _context.PurchaseOrderItems
                .AsNoTracking()
                .Include(poi => poi.Product)
                .Where(poi => poi.PurchaseOrderId == id)
                .ToListAsync();

            // Load Payments separately
            var payments = await _context.PurchaseOrderPayments
                .AsNoTracking()
                .Where(p => p.PurchaseOrderId == id)
                .ToListAsync();

            order.PurchaseOrderItems = purchaseOrderItems;
            order.Payments = payments;

            var warehouses = await _context.Warehouses
                .OrderBy(w => w.Name)
                .ToListAsync();

            ViewBag.Warehouses = warehouses;

            return View(order);
        }

        public IActionResult Create()
        {
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name");
            var products = _context.Products.Select(p => new {
                Value = p.Id,
                Text = p.Name + "|" + p.Price
            }).ToList();
            ViewBag.Products = new SelectList(products, "Value", "Text");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrder order, List<PurchaseOrderItem> PurchaseOrderItems)
        {
            // Remove validation for navigation properties
            ModelState.Remove("Supplier");
            ModelState.Remove("PurchaseOrderItems");
            ModelState.Remove("Payments");
            
            // Remove validation for PurchaseOrderItem navigation properties
            if (PurchaseOrderItems != null)
            {
                for (int i = 0; i < PurchaseOrderItems.Count; i++)
                {
                    ModelState.Remove($"PurchaseOrderItems[{i}].PurchaseOrder");
                    ModelState.Remove($"PurchaseOrderItems[{i}].Product");
                }
            }

            // Check if PurchaseOrderItems is null or empty
            if (PurchaseOrderItems == null || !PurchaseOrderItems.Any())
            {
                ModelState.AddModelError("", "يجب إضافة منتج واحد على الأقل");
            }

            if (ModelState.IsValid)
            {
                order.OrderDate = AppClock.UtcNow;
                order.Status = "جديد";
                
                // IMPORTANT: Clear any existing PurchaseOrderItems that may have been bound by model binding
                // This prevents duplication when we add items from the PurchaseOrderItems parameter
                order.PurchaseOrderItems = new List<PurchaseOrderItem>();

                // Add purchase order items from the parameter (not from model binding)
                if (PurchaseOrderItems != null && PurchaseOrderItems.Any())
                {
                    foreach (var item in PurchaseOrderItems)
                    {
                        if (item.ProductId > 0 && item.Quantity > 0)
                        {
                            // Create a new PurchaseOrderItem to avoid tracking issues
                            var newItem = new PurchaseOrderItem
                            {
                                ProductId = item.ProductId,
                                Quantity = item.Quantity,
                                UnitPrice = item.UnitPrice
                            };
                            order.PurchaseOrderItems.Add(newItem);
                        }
                    }
                }

                order.TotalAmount = order.PurchaseOrderItems.Any()
                    ? order.PurchaseOrderItems.Sum(i => i.Quantity * i.UnitPrice)
                    : 0;

                if (order.PaidAmount < 0)
                {
                    order.PaidAmount = 0;
                }

                if (order.PaidAmount > order.TotalAmount)
                {
                    order.PaidAmount = order.TotalAmount;
                }

                var outstandingAmount = order.TotalAmount - order.PaidAmount;

                // Update supplier account
                var supplier = await _context.Suppliers
                    .Include(s => s.Account)
                    .FirstOrDefaultAsync(s => s.Id == order.SupplierId);

                if (supplier != null)
                {
                    if (supplier.Account == null)
                    {
                        supplier.Account = new SupplierAccount
                        {
                            Supplier = supplier,
                            OpeningBalance = 0,
                            CurrentBalance = 0
                        };
                        _context.SupplierAccounts.Add(supplier.Account);
                    }

                    supplier.Account.CurrentBalance += outstandingAmount;
                }

                _context.Add(order);
                await _context.SaveChangesAsync();

                if (supplier?.Account != null && outstandingAmount > 0)
                {
                    _context.SupplierLedgerEntries.Add(new SupplierLedgerEntry
                    {
                        SupplierAccountId = supplier.Account.Id,
                        EntryDate = AppClock.UtcNow,
                        EntryType = "Debit",
                        Amount = outstandingAmount,
                        Description = $"أمر شراء رقم #{order.Id}",
                        PurchaseOrderId = order.Id
                    });

                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "تم إضافة أمر الشراء بنجاح";
                return RedirectToAction(nameof(Index));
            }
            
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name", order.SupplierId);
            var products = _context.Products.Select(p => new {
                Value = p.Id,
                Text = p.Name + "|" + p.Price
            }).ToList();
            ViewBag.Products = new SelectList(products, "Value", "Text");
            return View(order);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.PurchaseOrders.FindAsync(id);
            if (order == null) return NotFound();

            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name", order.SupplierId);
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PurchaseOrder order)
        {
            if (id != order.Id) return NotFound();

            // Remove validation for navigation properties
            ModelState.Remove("Supplier");
            ModelState.Remove("PurchaseOrderItems");
            ModelState.Remove("Payments");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PurchaseOrderExists(order.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name", order.SupplierId);
            return View(order);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.PurchaseOrders.FindAsync(id);
            if (order != null)
            {
                _context.PurchaseOrders.Remove(order);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id, Dictionary<int, int> itemWarehouseIds, Dictionary<int, DateTime?> itemExpiryDates)
        {
            var order = await _context.PurchaseOrders
                .FirstOrDefaultAsync(po => po.Id == id);

            if (order == null) return NotFound();

            var purchaseOrderItems = await _context.PurchaseOrderItems
                .Where(poi => poi.PurchaseOrderId == id)
                .ToListAsync();

            if (itemWarehouseIds == null || purchaseOrderItems.Any(item => !itemWarehouseIds.ContainsKey(item.Id)))
            {
                TempData["ErrorMessage"] = "الرجاء اختيار المستودع لكل صنف قبل تأكيد الاستلام";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (itemWarehouseIds.Values.Any(value => value <= 0))
            {
                TempData["ErrorMessage"] = "تم اختيار مستودع غير صالح";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (itemExpiryDates == null || purchaseOrderItems.Any(item => !itemExpiryDates.TryGetValue(item.Id, out var expiry) || !expiry.HasValue))
            {
                TempData["ErrorMessage"] = "الرجاء إدخال تاريخ الصلاحية لكل صنف قبل تأكيد الاستلام";
                return RedirectToAction(nameof(Details), new { id });
            }

            var warehouseIds = itemWarehouseIds.Values.Distinct().ToList();

            var validWarehouseCount = await _context.Warehouses
                .CountAsync(w => warehouseIds.Contains(w.Id));

            if (validWarehouseCount != warehouseIds.Count)
            {
                TempData["ErrorMessage"] = "تم اختيار مستودع غير صالح";
                return RedirectToAction(nameof(Details), new { id });
            }

            var productIds = purchaseOrderItems
                .Select(poi => poi.ProductId)
                .Distinct()
                .ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var inventories = await _context.ProductInventories
                .Where(pi => productIds.Contains(pi.ProductId) && warehouseIds.Contains(pi.WarehouseId))
                .ToListAsync();

            foreach (var item in purchaseOrderItems)
            {
                var targetWarehouseId = itemWarehouseIds[item.Id];
                var expiryDate = itemExpiryDates[item.Id]!.Value.Date;

                if (products.TryGetValue(item.ProductId, out var product))
                {
                    if (item.UnitPrice != product.Price)
                    {
                        product.Price = item.UnitPrice;
                    }

                    if (!product.ExpiryDate.HasValue || expiryDate < product.ExpiryDate.Value)
                    {
                        product.ExpiryDate = expiryDate;
                    }
                }

                var inventory = inventories.FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == targetWarehouseId);

                if (inventory != null)
                {
                    inventory.OnHandQuantity += item.Quantity;
                    inventory.LastUpdated = AppClock.UtcNow;
                    if (!inventory.NextExpiryDate.HasValue || expiryDate < inventory.NextExpiryDate.Value)
                    {
                        inventory.NextExpiryDate = expiryDate;
                    }
                }
                else
                {
                    inventory = new ProductInventory
                    {
                        ProductId = item.ProductId,
                        WarehouseId = targetWarehouseId,
                        OnHandQuantity = item.Quantity,
                        ReservedQuantity = 0,
                        ReorderPoint = 0,
                        LastUpdated = AppClock.UtcNow,
                        NextExpiryDate = expiryDate
                    };
                    inventories.Add(inventory);
                    _context.ProductInventories.Add(inventory);
                }

                _context.StockMovements.Add(new StockMovement
                {
                    ProductId = item.ProductId,
                    WarehouseId = targetWarehouseId,
                    MovementType = "إدخال",
                    Quantity = item.Quantity,
                    MovementDate = AppClock.UtcNow,
                    Reason = "استلام أمر شراء",
                    ReferenceNumber = $"PO-{order.Id}"
                });
            }

            order.Status = "مستلم";
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddPayment(int id, decimal amount, string method)
        {
            // Load order with supplier
            var order = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (order == null) return NotFound();

            // Add payment record
            _context.PurchaseOrderPayments.Add(new PurchaseOrderPayment
            {
                PurchaseOrderId = id,
                PaymentDate = AppClock.UtcNow,
                Amount = amount,
                Method = method
            });

            order.PaidAmount += amount;

            // Update supplier account
            if (order.Supplier?.Account != null)
            {
                order.Supplier.Account.CurrentBalance -= amount;

                // Add ledger entry
                _context.SupplierLedgerEntries.Add(new SupplierLedgerEntry
                {
                    SupplierAccountId = order.Supplier.Account.Id,
                    EntryDate = AppClock.UtcNow,
                    EntryType = "Credit",
                    Amount = amount,
                    Description = $"دفعة لأمر شراء #{id}",
                    PurchaseOrderId = id
                });
            }

            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"تم إضافة دفعة بمبلغ {amount:N2} جنيه للمورد بنجاح";
            
            // Check if coming from supplier details
            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("/Suppliers/Details"))
            {
                var supplierId = order.SupplierId;
                return RedirectToAction("Details", "Suppliers", new { id = supplierId });
            }
            
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> PrintInvoice(int? id)
        {
            if (id == null) return NotFound();

            // Load order without PurchaseOrderItems to prevent duplication
            var order = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(po => po.Supplier)
                .Where(m => m.Id == id)
                .Select(po => new PurchaseOrder
                {
                    Id = po.Id,
                    SupplierId = po.SupplierId,
                    Supplier = po.Supplier,
                    OrderDate = po.OrderDate,
                    TotalAmount = po.TotalAmount,
                    PaidAmount = po.PaidAmount,
                    Status = po.Status,
                    PurchaseOrderItems = new List<PurchaseOrderItem>()
                })
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            // Load PurchaseOrderItems separately with explicit query
            var purchaseOrderItems = await _context.PurchaseOrderItems
                .AsNoTracking()
                .Include(poi => poi.Product)
                .Where(poi => poi.PurchaseOrderId == id)
                .ToListAsync();

            order.PurchaseOrderItems = purchaseOrderItems;

            // Load store settings
            var storeSettings = await _context.StoreSettings.FirstOrDefaultAsync();
            if (storeSettings == null)
            {
                storeSettings = new StoreSettings { StoreName = "نظام إدارة المخازن" };
            }
            ViewBag.StoreSettings = storeSettings;

            return View(order);
        }

        private bool PurchaseOrderExists(int id)
        {
            return _context.PurchaseOrders.Any(e => e.Id == id);
        }
    }
}
