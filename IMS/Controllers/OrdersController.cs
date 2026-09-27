using System;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Accountant + "," + RoleNamesConstants.Pharmacist + "," + RoleNamesConstants.Cashier)]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInsuranceService _insuranceService;
        private readonly INotificationService _notificationService;
        private readonly IInvoiceService _invoiceService;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrdersController(ApplicationDbContext context, IInsuranceService insuranceService, INotificationService notificationService, IInvoiceService invoiceService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _insuranceService = insuranceService;
            _notificationService = notificationService;
            _invoiceService = invoiceService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string searchTerm)
        {
            var userString = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userString ?? "");
            var branchId = user?.BranchId;

            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .AsQueryable();

            if (branchId.HasValue && !User.IsInRole(RoleNamesConstants.Admin))
            {
                ordersQuery = ordersQuery.Where(o => o.BranchId == branchId);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                ordersQuery = ordersQuery.Where(o => 
                    o.Id.ToString().Contains(searchTerm) ||
                    o.Customer.Name.Contains(searchTerm) ||
                    o.Customer.PhoneNumber.Contains(searchTerm) ||
                    o.Status.Contains(searchTerm)
                );
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            
            ViewBag.SearchTerm = searchTerm;
            return View(orders);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            // Load order with customer and insurance policy
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.InsurancePolicy)
                    .ThenInclude(p => p.Insurance)
                .Include(o => o.DiscountCode)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            var orderItems = await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Product)
                .Where(oi => oi.OrderId == id)
                .ToListAsync();

            order.OrderItems = orderItems;

            var paymentSchedule = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.OrderId == id && t.TransactionType == "سداد")
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            ViewBag.PaymentSchedule = paymentSchedule;

            return View(order);
        }

        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name");
            ViewData["InsurancePolicyId"] = new SelectList(new List<InsurancePolicy>(), "Id", "PolicyNumber");
            var products = _context.Products
                .Include(p => p.Category)
                .Select(p => new {
                    Value = p.Id,
                    Text = (p.Category != null ? p.Category.Name + " - " : "") + p.Name + "|" + (p.SalePrice ?? 0)
                }).ToList();
            ViewBag.Products = new SelectList(products, "Value", "Text");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order, List<OrderItem> OrderItems)
        {
            // Remove validation for navigation properties
            ModelState.Remove("Customer");
            ModelState.Remove("OrderItems");
            
            // Remove validation for OrderItem navigation properties
            if (OrderItems != null)
            {
                for (int i = 0; i < OrderItems.Count; i++)
                {
                    ModelState.Remove($"OrderItems[{i}].Order");
                    ModelState.Remove($"OrderItems[{i}].Product");
                }
            }

            // Check if OrderItems is null or empty
            if (OrderItems == null || !OrderItems.Any())
            {
                ModelState.AddModelError("", "يجب إضافة منتج واحد على الأقل");
            }

            if (ModelState.IsValid)
            {
                // === تحققات الأمان الحرجة ===
                if (OrderItems != null && OrderItems.Any())
                {
                    var itemProductIds = OrderItems.Where(i => i.ProductId > 0).Select(i => i.ProductId).Distinct().ToList();
                    var productsForValidation = await _context.Products
                        .Where(p => itemProductIds.Contains(p.Id))
                        .ToListAsync();

                    // 1. منع بيع الأدوية منتهية الصلاحية
                    var expiredProducts = productsForValidation
                        .Where(p => p.ExpiryDate.HasValue && p.ExpiryDate.Value < AppClock.UtcNow)
                        .ToList();
                    foreach (var ep in expiredProducts)
                    {
                        ModelState.AddModelError("", $"⛔ لا يمكن بيع الدواء '{ep.Name}' - منتهي الصلاحية منذ {ep.ExpiryDate:yyyy-MM-dd}");
                    }

                    // 2. فرض الوصفة الطبية للأدوية المقيدة
                    var rxProducts = productsForValidation
                        .Where(p => p.RequiresPrescription)
                        .ToList();
                    if (rxProducts.Any())
                    {
                        var rxNames = string.Join("، ", rxProducts.Select(p => p.Name));
                        ModelState.AddModelError("", $"⚠️ الأدوية التالية تتطلب وصفة طبية: {rxNames}. يرجى صرفها عبر شاشة الوصفات الطبية.");
                    }

                    // 3. فحص التفاعلات الدوائية بين أصناف الطلب
                    if (itemProductIds.Count > 1)
                    {
                        var interactions = await _context.DrugInteractions
                            .Include(di => di.Product1)
                            .Include(di => di.Product2)
                            .Where(di => itemProductIds.Contains(di.Product1Id) && itemProductIds.Contains(di.Product2Id))
                            .ToListAsync();

                        foreach (var interaction in interactions)
                        {
                            ModelState.AddModelError("", $"⚠️ تفاعل دوائي: {interaction.Product1?.Name} مع {interaction.Product2?.Name} - {interaction.InteractionDescription}");
                        }
                    }

                    // 4. فحص حساسية العميل
                    var customer = await _context.Customers.FindAsync(order.CustomerId);
                    if (customer != null && !string.IsNullOrWhiteSpace(customer.Allergies))
                    {
                        var allergyList = customer.Allergies.Split(',', '،', ';')
                            .Select(a => a.Trim().ToLower())
                            .Where(a => !string.IsNullOrEmpty(a))
                            .ToList();

                        foreach (var product in productsForValidation)
                        {
                            foreach (var allergy in allergyList)
                            {
                                if ((!string.IsNullOrEmpty(product.Name) && product.Name.ToLower().Contains(allergy)) ||
                                    (!string.IsNullOrEmpty(product.ActiveIngredient) && product.ActiveIngredient.ToLower().Contains(allergy)) ||
                                    (!string.IsNullOrEmpty(product.ScientificName) && product.ScientificName.ToLower().Contains(allergy)))
                                {
                                    ModelState.AddModelError("", $"🚨 تحذير حساسية: العميل '{customer.Name}' لديه حساسية تجاه '{allergy}' - الدواء '{product.Name}' قد يسبب رد فعل تحسسي!");
                                }
                            }
                        }
                    }
                }

                if (!ModelState.IsValid)
                {
                    ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", order.CustomerId);
                    var prods = _context.Products
                        .Include(p => p.Category)
                        .Select(p => new {
                            Value = p.Id,
                            Text = (p.Category != null ? p.Category.Name + " - " : "") + p.Name + "|" + (p.SalePrice ?? 0)
                        }).ToList();
                    ViewBag.Products = new SelectList(prods, "Value", "Text");
                    return View(order);
                }

                var userString = _userManager.GetUserId(User);
                var user = await _userManager.FindByIdAsync(userString ?? "");
                order.BranchId = user?.BranchId;

                order.OrderDate = AppClock.UtcNow;
                order.Status = "جديد";
                
                // IMPORTANT: Clear any existing OrderItems that may have been bound by model binding
                // This prevents duplication when we add items from the OrderItems parameter
                order.OrderItems = new List<OrderItem>();

                // Add order items from the parameter (not from model binding)
                if (OrderItems != null && OrderItems.Any())
                {
                    foreach (var item in OrderItems)
                    {
                        if (item.ProductId > 0 && item.Quantity > 0)
                        {
                            // Create a new OrderItem to avoid tracking issues
                            var newItem = new OrderItem
                            {
                                ProductId = item.ProductId,
                                Quantity = item.Quantity,
                                UnitPrice = item.UnitPrice,
                                DiscountPercentage = item.DiscountPercentage
                            };
                            order.OrderItems.Add(newItem);
                        }
                    }
                }

                // Recalculate TotalAmount on the server to ensure integrity
                if (order.OrderItems.Any())
                {
                    order.TotalAmount = order.OrderItems
                        .Sum(oi =>
                            (oi.Quantity * oi.UnitPrice)
                            - ((oi.Quantity * oi.UnitPrice) * (oi.DiscountPercentage / 100))
                        );
                }

                // Handle Discount Code (Coupon)
                if (order.DiscountCodeId.HasValue)
                {
                    var discountCode = await _context.DiscountCodes.FindAsync(order.DiscountCodeId.Value);
                    if (discountCode != null && discountCode.IsActive && discountCode.StartDate <= AppClock.UtcNow && discountCode.EndDate >= AppClock.UtcNow)
                    {
                        if (discountCode.CurrentUsage < discountCode.UsageLimit && order.TotalAmount >= discountCode.MinimumOrderAmount)
                        {
                            decimal serverDiscountAmount = 0;
                            if (discountCode.Percentage.HasValue)
                            {
                                serverDiscountAmount = order.TotalAmount * (discountCode.Percentage.Value / 100);
                            }
                            else if (discountCode.FixedAmount.HasValue)
                            {
                                serverDiscountAmount = discountCode.FixedAmount.Value;
                            }
                            
                            order.DiscountAmount = Math.Round(serverDiscountAmount, 2);
                            order.TotalAmount -= order.DiscountAmount;
                            if (order.TotalAmount < 0) order.TotalAmount = 0;
                        }
                        else
                        {
                            order.DiscountCodeId = null;
                            order.DiscountAmount = 0;
                        }
                    }
                    else
                    {
                        order.DiscountCodeId = null;
                        order.DiscountAmount = 0;
                    }
                }

                // Calculate insurance coverage if policy is selected
                if (order.InsurancePolicyId.HasValue)
                {
                    var insuranceResult = await _insuranceService.CalculateInsuranceCoverageAsync(
                        order.InsurancePolicyId, order.TotalAmount);

                    if (insuranceResult.IsValid)
                    {
                        order.InsuranceCoverageAmount = insuranceResult.InsuranceCoverageAmount;
                        order.CustomerPayableAmount = insuranceResult.CustomerPayableAmount;
                        order.AppliedCoveragePercentage = insuranceResult.AppliedCoveragePercentage;

                        // Update the policy used amount
                        await _insuranceService.UpdatePolicyUsedAmountAsync(
                            order.InsurancePolicyId.Value, 
                            order.InsuranceCoverageAmount);
                    }
                    else
                    {
                        // If insurance calculation failed, clear insurance fields
                        order.InsurancePolicyId = null;
                        order.CustomerPayableAmount = order.TotalAmount;
                        TempData["WarningMessage"] = insuranceResult.ErrorMessage;
                    }
                }
                else
                {
                    order.CustomerPayableAmount = order.TotalAmount;
                }

                var inventoryErrors = await ValidateInventoryAvailabilityAsync(order);
                if (inventoryErrors.Any())
                {
                    foreach (var error in inventoryErrors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                }
                else
                {
                    var strategy = _context.Database.CreateExecutionStrategy();
                    await strategy.ExecuteAsync(async () =>
                    {
                        await using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            _context.Add(order);
                            await _context.SaveChangesAsync();

                            // Update Discount Code usage
                            if (order.DiscountCodeId.HasValue)
                            {
                                var discount = await _context.DiscountCodes.FindAsync(order.DiscountCodeId.Value);
                                if (discount != null)
                                {
                                    discount.CurrentUsage++;
                                }
                            }

                            await DeductInventoryForOrderAsync(order);
                            await _context.SaveChangesAsync();

                            await transaction.CommitAsync();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });

                    TempData["SuccessMessage"] = "تم إضافة الطلب بنجاح";
                    return RedirectToAction(nameof(Index));
                }
            }
            
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", order.CustomerId);
            var products = _context.Products
                .Include(p => p.Category)
                .Select(p => new {
                    Value = p.Id,
                    Text = (p.Category != null ? p.Category.Name + " - " : "") + p.Name + "|" + (p.SalePrice ?? 0)
                }).ToList();
            ViewBag.Products = new SelectList(products, "Value", "Text");
            return View(order);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", order.CustomerId);
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Order order)
        {
            if (id != order.Id) return NotFound();

            // Remove validation for navigation properties
            ModelState.Remove("Customer");
            ModelState.Remove("OrderItems");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", order.CustomerId);
            return View(order);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var referenceNumber = $"ORD-{order.Id}";
                var movements = await _context.StockMovements
                    .Where(sm => sm.ReferenceNumber == referenceNumber && sm.MovementType == "إخراج")
                    .ToListAsync();

                if (movements.Any())
                {
                    var productIds = movements.Select(sm => sm.ProductId).Distinct().ToList();
                    var warehouseIds = movements.Select(sm => sm.WarehouseId).Distinct().ToList();

                    var inventories = await _context.ProductInventories
                        .Where(pi => productIds.Contains(pi.ProductId) && warehouseIds.Contains(pi.WarehouseId))
                        .ToListAsync();

                    var inventoryLookup = inventories.ToDictionary(pi => (pi.ProductId, pi.WarehouseId));
                    var timestamp = AppClock.UtcNow;
                    var reversalMovements = new List<StockMovement>();

                    foreach (var movement in movements)
                    {
                        if (inventoryLookup.TryGetValue((movement.ProductId, movement.WarehouseId), out var inventory))
                        {
                            inventory.OnHandQuantity += movement.Quantity;
                            inventory.LastUpdated = timestamp;
                        }

                        reversalMovements.Add(new StockMovement
                        {
                            ProductId = movement.ProductId,
                            WarehouseId = movement.WarehouseId,
                            MovementType = "إدخال",
                            Quantity = movement.Quantity,
                            MovementDate = timestamp,
                            Reason = "إلغاء طلب",
                            ReferenceNumber = $"ORD-CANCEL-{order.Id}"
                        });
                    }

                    if (reversalMovements.Count > 0)
                    {
                        await _context.StockMovements.AddRangeAsync(reversalMovements);
                    }
                }

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            });

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = status;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddPayment(int id, decimal amount)
        {
            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "المبلغ المدفوع يجب أن يكون أكبر من صفر";
                return RedirectToAction(nameof(Details), new { id });
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            var targetAmount = order.CustomerPayableAmount > 0 ? order.CustomerPayableAmount : order.TotalAmount;
            var outstanding = targetAmount - order.PaidAmount;

            if (outstanding <= 0)
            {
                TempData["ErrorMessage"] = "لا توجد مبالغ مستحقة على هذا الطلب";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (amount > outstanding)
            {
                TempData["ErrorMessage"] = $"المبلغ يتجاوز المتبقي ({outstanding:N2} جنيه)";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.PaidAmount += amount;

            if (order.PaidAmount >= targetAmount && !string.Equals(order.Status, "مكتمل", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "مكتمل";
            }

            _context.Transactions.Add(new Transaction
            {
                TransactionDate = AppClock.UtcNow,
                TransactionType = "سداد",
                Amount = amount,
                OrderId = order.Id,
                Description = $"دفعة للطلب رقم {order.Id}"
            });

            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"تم إضافة دفعة بمبلغ {amount:N2} جنيه بنجاح";
            
            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("/Customers/Details"))
            {
                var customerId = order.CustomerId;
                return RedirectToAction("Details", "Customers", new { id = customerId });
            }
            
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> PrintInvoice(int? id)
        {
            if (id == null) return NotFound();

            // Load order without OrderItems to prevent duplication
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(m => m.Id == id)
                .Select(o => new Order
                {
                    Id = o.Id,
                    CustomerId = o.CustomerId,
                    Customer = o.Customer,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    PaidAmount = o.PaidAmount,
                    Status = o.Status,
                    OrderItems = new List<OrderItem>()
                })
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            // Load OrderItems separately with explicit query
            var orderItems = await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Product)
                .Where(oi => oi.OrderId == id)
                .ToListAsync();

            order.OrderItems = orderItems;

            // Load store settings
            var storeSettings = await _context.StoreSettings.FirstOrDefaultAsync();
            if (storeSettings == null)
            {
                storeSettings = new StoreSettings { StoreName = "نظام إدارة المخازن" };
            }
            ViewBag.StoreSettings = storeSettings;

            return View(order);
        }

        public async Task<IActionResult> DownloadInvoice(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            var storeSettings = await _context.StoreSettings.FirstOrDefaultAsync() 
                               ?? new StoreSettings { StoreName = "صيدلية النظام" };

            var pdfBytes = _invoiceService.GenerateInvoicePdf(order, storeSettings);
            
            return File(pdfBytes, "application/pdf", $"Invoice_{order.Id}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // AJAX: Get valid insurance policies for customer
        [HttpGet]
        public async Task<IActionResult> GetCustomerPolicies(int customerId)
        {
            var policies = await _insuranceService.GetValidPoliciesForCustomerAsync(customerId);
            var policyList = policies.Select(p => new
            {
                id = p.Id,
                policyNumber = p.PolicyNumber,
                insuranceName = p.Insurance?.Name,
                coveragePercentage = p.CoveragePercentage,
                remainingAmount = p.RemainingAmount,
                text = $"{p.PolicyNumber} - {p.Insurance?.Name} ({p.CoveragePercentage}%)"
            });
            return Json(policyList);
        }

        // AJAX: Calculate insurance coverage
        [HttpPost]
        public async Task<IActionResult> CalculateInsurance(int? policyId, decimal totalAmount)
        {
            var result = await _insuranceService.CalculateInsuranceCoverageAsync(policyId, totalAmount);
            return Json(new
            {
                success = result.IsValid,
                insuranceCoverageAmount = result.InsuranceCoverageAmount,
                customerPayableAmount = result.CustomerPayableAmount,
                appliedCoveragePercentage = result.AppliedCoveragePercentage,
                errorMessage = result.ErrorMessage
            });
        }

        // AJAX: فحص سلامة الدواء قبل الإضافة في POS
        [HttpGet]
        public async Task<IActionResult> CheckProductSafety(int productId, int customerId)
        {
            var warnings = new List<string>();
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
                return Json(new { warnings });

            // فحص انتهاء الصلاحية
            if (product.ExpiryDate.HasValue && product.ExpiryDate.Value < AppClock.UtcNow)
                warnings.Add($"⛔ هذا الدواء منتهي الصلاحية منذ {product.ExpiryDate:yyyy-MM-dd}");
            else if (product.IsNearExpiry)
                warnings.Add($"⚠️ هذا الدواء قارب على الانتهاء (صالح حتى {product.ExpiryDate:yyyy-MM-dd})");

            // فحص الوصفة الطبية
            if (product.RequiresPrescription)
                warnings.Add("⚠️ هذا الدواء يتطلب وصفة طبية");

            // فحص حساسية العميل
            if (customerId > 0)
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer != null && !string.IsNullOrWhiteSpace(customer.Allergies))
                {
                    var allergyList = customer.Allergies.Split(',', '،', ';')
                        .Select(a => a.Trim().ToLower())
                        .Where(a => !string.IsNullOrEmpty(a))
                        .ToList();

                    foreach (var allergy in allergyList)
                    {
                        if ((!string.IsNullOrEmpty(product.Name) && product.Name.ToLower().Contains(allergy)) ||
                            (!string.IsNullOrEmpty(product.ActiveIngredient) && product.ActiveIngredient.ToLower().Contains(allergy)) ||
                            (!string.IsNullOrEmpty(product.ScientificName) && product.ScientificName.ToLower().Contains(allergy)))
                        {
                            warnings.Add($"🚨 تحذير حساسية: العميل لديه حساسية تجاه '{allergy}'");
                        }
                    }
                }
            }

            return Json(new { warnings, isExpired = product.IsExpired, requiresPrescription = product.RequiresPrescription });
        }

        private async Task<List<string>> ValidateInventoryAvailabilityAsync(Order order)
        {
            var errors = new List<string>();

            if (order == null || !order.OrderItems.Any())
            {
                return errors;
            }

            var productQuantities = order.OrderItems
                .GroupBy(item => item.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToList();

            var productIds = productQuantities
                .Select(item => item.ProductId)
                .ToList();

            if (!productIds.Any())
            {
                return errors;
            }

            var userString = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userString ?? "");
            var branchId = user?.BranchId;

            var inventoryTotals = await _context.ProductInventories
                .Include(pi => pi.Warehouse)
                .Where(pi => productIds.Contains(pi.ProductId) && (!branchId.HasValue || pi.Warehouse.BranchId == branchId))
                .GroupBy(pi => pi.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(pi => pi.OnHandQuantity)
                })
                .ToDictionaryAsync(item => item.ProductId, item => item.Quantity);

            var productNames = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            foreach (var item in productQuantities)
            {
                var available = inventoryTotals.TryGetValue(item.ProductId, out var quantity) ? quantity : 0;
                if (available < item.Quantity)
                {
                    var productName = productNames.TryGetValue(item.ProductId, out var name) ? name : $"منتج رقم {item.ProductId}";
                    errors.Add($"الكمية المتاحة من {productName} لا تكفي للطلب ({available:N2} متاح مقابل {item.Quantity}).");
                }
            }

            return errors;
        }

        private async Task DeductInventoryForOrderAsync(Order order)
        {
            if (order == null || !order.OrderItems.Any())
            {
                return;
            }

            var productIds = order.OrderItems
                .Select(item => item.ProductId)
                .Distinct()
                .ToList();

            var inventories = await _context.ProductInventories
                .Include(pi => pi.Warehouse)
                .Where(pi => productIds.Contains(pi.ProductId) && (!order.BranchId.HasValue || pi.Warehouse.BranchId == order.BranchId))
                .ToListAsync();

            var inventoryLookup = inventories
                .GroupBy(pi => pi.ProductId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(pi => pi.OnHandQuantity).ToList());

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var referenceNumber = $"ORD-{order.Id}";
            var timestamp = AppClock.UtcNow;

            foreach (var group in order.OrderItems.GroupBy(item => item.ProductId))
            {
                var remainingQuantity = group.Sum(item => (decimal)item.Quantity);

                if (!inventoryLookup.TryGetValue(group.Key, out var productInventories) || productInventories.Count == 0)
                {
                    throw new InvalidOperationException("تم اكتشاف نقص في بيانات المخزون أثناء الخصم.");
                }

                foreach (var inventory in productInventories)
                {
                    if (remainingQuantity <= 0)
                    {
                        break;
                    }

                    if (inventory.OnHandQuantity <= 0)
                    {
                        continue;
                    }

                    var deduction = Math.Min(remainingQuantity, inventory.OnHandQuantity);
                    if (deduction <= 0)
                    {
                        continue;
                    }

                    inventory.OnHandQuantity -= deduction;
                    inventory.LastUpdated = timestamp;
                    if (inventory.OnHandQuantity <= 0)
                    {
                        inventory.NextExpiryDate = null;
                    }

                    // Low Stock Check
                    var targetProduct = products[group.Key];
                    var totalStock = _context.ProductInventories
                        .Where(pi => pi.ProductId == targetProduct.Id)
                        .Sum(pi => pi.OnHandQuantity);

                    if (targetProduct.LowStockThreshold.HasValue && totalStock <= targetProduct.LowStockThreshold.Value)
                    {
                        await _notificationService.CreateNotificationAsync(
                            "تنبيه نقص مخزون",
                            $"الدواء '{targetProduct.Name}' وصل للحد الأدنى للمخزون (المتبقي: {totalStock:N2})",
                            "Warning",
                            null,
                            $"/Inventory/Details/{targetProduct.Id}"
                        );
                    }

                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductId = group.Key,
                        WarehouseId = inventory.WarehouseId,
                        Quantity = -deduction,
                        MovementType = "إخراج",
                        ReferenceNumber = referenceNumber,
                        MovementDate = timestamp,
                        BranchId = order.BranchId,
                        Reason = $"مبيع فاتورة رقم #{order.Id}"
                    });

                    remainingQuantity -= deduction;
                }

                if (remainingQuantity > 0)
                {
                    throw new InvalidOperationException("تم اكتشاف نقص في المخزون أثناء معالجة الطلب.");
                }

                if (products.TryGetValue(group.Key, out var product))
                {
                    var remainingExpiries = inventoryLookup[group.Key]
                        .Where(i => i.OnHandQuantity > 0 && i.NextExpiryDate.HasValue)
                        .Select(i => i.NextExpiryDate!.Value)
                        .ToList();

                    product.ExpiryDate = remainingExpiries.Any()
                        ? remainingExpiries.Min()
                        : null;
                }
            }
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }
    }
}
