using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IMS.Controllers;
using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace IMS.Tests;

public class InventoryBehaviourTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Xunit.Fact]
    public async Task PurchaseOrdersController_Receive_SetsNextExpiryDate()
    {
        using var context = CreateContext();

        var branch = new Branch { Id = 1, Name = "الفرع الرئيسي", Code = "BR-1" };
        var warehouse = new Warehouse { Id = 1, Name = "المستودع", BranchId = branch.Id, Branch = branch };
        var supplier = new Supplier { Id = 1, Name = "المورد" };
        var product = new Product { Id = 1, Name = "المنتج" };

        context.Branches.Add(branch);
        context.Warehouses.Add(warehouse);
        context.Suppliers.Add(supplier);
        context.Products.Add(product);

        var order = new PurchaseOrder
        {
            SupplierId = supplier.Id,
            OrderDate = AppClock.UtcNow,
            Status = "جديد",
            TotalAmount = 100,
            PaidAmount = 0,
            PurchaseOrderItems = new List<PurchaseOrderItem>
            {
                new PurchaseOrderItem
                {
                    ProductId = product.Id,
                    Quantity = 5,
                    UnitPrice = 20
                }
            }
        };

        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();

        var item = order.PurchaseOrderItems.First();
        var expiryDate = AppClock.UtcNow.Date.AddDays(45);

        var controller = new PurchaseOrdersController(context);

        var result = await controller.Receive(
            order.Id,
            new Dictionary<int, int> { { item.Id, warehouse.Id } },
            new Dictionary<int, DateTime?> { { item.Id, expiryDate } });

        Xunit.Assert.IsType<RedirectToActionResult>(result);

        var inventory = await context.ProductInventories.SingleAsync();
        Xunit.Assert.Equal(warehouse.Id, inventory.WarehouseId);
        Xunit.Assert.Equal(expiryDate, inventory.NextExpiryDate);

        var updatedOrder = await context.PurchaseOrders.FindAsync(order.Id);
        Xunit.Assert.Equal("مستلم", updatedOrder!.Status);

        var updatedProduct = await context.Products.FindAsync(product.Id);
        Xunit.Assert.Equal(expiryDate, updatedProduct!.ExpiryDate);
    }

    [Xunit.Fact]
    public async Task ExpiryAlertService_GenerateAlertsAsync_UsesInventoryExpiry()
    {
        using var context = CreateContext();

        var branch = new Branch { Id = 1, Name = "الفرع", Code = "BR-2" };
        var warehouse = new Warehouse { Id = 1, Name = "مستودع", BranchId = branch.Id, Branch = branch };
        var product = new Product { Id = 1, Name = "منتج", ExpiryDate = AppClock.UtcNow.Date.AddMonths(6) };
        var expiryDate = AppClock.UtcNow.Date.AddDays(20);

        context.Branches.Add(branch);
        context.Warehouses.Add(warehouse);
        context.Products.Add(product);
        context.ProductInventories.Add(new ProductInventory
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQuantity = 12,
            ReservedQuantity = 0,
            ReorderPoint = 0,
            LastUpdated = AppClock.UtcNow,
            NextExpiryDate = expiryDate
        });

        await context.SaveChangesAsync();

        var service = new ExpiryAlertService(context);

        var firstRun = await service.GenerateAlertsAsync();
        Xunit.Assert.Equal(1, firstRun);

        var alert = await context.ExpiryAlerts.SingleAsync();
        Xunit.Assert.Equal(product.Id, alert.ProductId);
        Xunit.Assert.Equal(warehouse.Id, alert.WarehouseId);
        Xunit.Assert.Equal(expiryDate, alert.ExpiryDate);
        Xunit.Assert.Equal("قريب من الانتهاء", alert.AlertStatus);

        var secondRun = await service.GenerateAlertsAsync();
        Xunit.Assert.Equal(0, secondRun);
    }

    [Xunit.Fact]
    public async Task OrdersController_AddPayment_Rejects_Overpayment()
    {
        using var context = CreateContext();

        context.Customers.Add(new Customer { Id = 1, Name = "عميل" });
        context.Orders.Add(new Order
        {
            Id = 1,
            CustomerId = 1,
            TotalAmount = 100,
            CustomerPayableAmount = 100,
            PaidAmount = 90
        });
        await context.SaveChangesAsync();

        var controller = CreateOrdersController(context);

        var result = await controller.AddPayment(1, 20);

        var redirect = Xunit.Assert.IsType<RedirectToActionResult>(result);
        Xunit.Assert.Equal("Details", redirect.ActionName);

        var order = await context.Orders.FindAsync(1);
        Xunit.Assert.Equal(90, order!.PaidAmount);
        Xunit.Assert.NotNull(controller.TempData["ErrorMessage"]);
        Xunit.Assert.Empty(context.Transactions);
    }

    [Xunit.Fact]
    public async Task OrdersController_AddPayment_Records_Transaction()
    {
        using var context = CreateContext();

        context.Customers.Add(new Customer { Id = 1, Name = "عميل" });
        context.Orders.Add(new Order
        {
            Id = 1,
            CustomerId = 1,
            TotalAmount = 100,
            CustomerPayableAmount = 100
        });
        await context.SaveChangesAsync();

        var controller = CreateOrdersController(context);

        var result = await controller.AddPayment(1, 100);

        var redirect = Xunit.Assert.IsType<RedirectToActionResult>(result);
        Xunit.Assert.Equal("Details", redirect.ActionName);

        var order = await context.Orders.FindAsync(1);
        Xunit.Assert.Equal(100, order!.PaidAmount);
        Xunit.Assert.Equal("مكتمل", order.Status);

        var transaction = await context.Transactions.SingleAsync();
        Xunit.Assert.Equal(100, transaction.Amount);
        Xunit.Assert.Equal(1, transaction.OrderId);
        Xunit.Assert.Equal("سداد", transaction.TransactionType);
    }

    [Xunit.Fact]
    public async Task OrdersController_DeleteConfirmed_RestoresInventory()
    {
        using var context = CreateContext();

        var branch = new Branch { Id = 1, Name = "الفرع", Code = "BR-1" };
        context.Branches.Add(branch);
        context.Customers.Add(new Customer { Id = 1, Name = "عميل" });
        context.Products.Add(new Product { Id = 1, Name = "منتج" });
        context.Warehouses.Add(new Warehouse { Id = 1, Name = "مستودع", BranchId = branch.Id, Branch = branch });
        await context.SaveChangesAsync();

        var order = new Order
        {
            Id = 1,
            CustomerId = 1,
            TotalAmount = 20,
            CustomerPayableAmount = 20,
            PaidAmount = 20,
            Status = "مكتمل"
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        context.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = 1,
            Quantity = 2,
            UnitPrice = 10,
            DiscountPercentage = 0
        });

        context.ProductInventories.Add(new ProductInventory
        {
            ProductId = 1,
            WarehouseId = 1,
            OnHandQuantity = 0,
            ReservedQuantity = 0,
            ReorderPoint = 0,
            LastUpdated = AppClock.UtcNow
        });

        context.StockMovements.Add(new StockMovement
        {
            ProductId = 1,
            WarehouseId = 1,
            MovementType = "إخراج",
            Quantity = 2,
            MovementDate = AppClock.UtcNow,
            Reason = "بيع للعميل",
            ReferenceNumber = "ORD-1"
        });

        await context.SaveChangesAsync();

        var controller = CreateOrdersController(context);
        var result = await controller.DeleteConfirmed(order.Id);

        var redirect = Xunit.Assert.IsType<RedirectToActionResult>(result);
        Xunit.Assert.Equal("Index", redirect.ActionName);
        Xunit.Assert.False(await context.Orders.AnyAsync(o => o.Id == order.Id));

        var inventory = await context.ProductInventories.SingleAsync();
        Xunit.Assert.Equal(2, inventory.OnHandQuantity);

        var reversalMovement = await context.StockMovements.SingleAsync(sm => sm.ReferenceNumber == "ORD-CANCEL-1");
        Xunit.Assert.Equal("إدخال", reversalMovement.MovementType);
        Xunit.Assert.Equal(2, reversalMovement.Quantity);
    }

    [Xunit.Fact]
    public async Task ReturnProcessingService_CreateAsync_RespectsExistingReturns()
    {
        using var context = CreateContext();

        var customer = new Customer { Id = 1, Name = "عميل" };
        var product = new Product { Id = 1, Name = "منتج" };
        context.Customers.Add(customer);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var order = new Order
        {
            CustomerId = customer.Id,
            OrderDate = AppClock.UtcNow,
            TotalAmount = 50,
            CustomerPayableAmount = 50,
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = 5,
                    UnitPrice = 10,
                    DiscountPercentage = 0
                }
            }
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var orderItemId = order.OrderItems.First().Id;

        context.Returns.Add(new Return
        {
            OrderId = order.Id,
            CustomerId = customer.Id,
            ReturnDate = AppClock.UtcNow,
            Status = ReturnStatusConstants.Approved,
            ReturnType = ReturnTypeConstants.Partial,
            Reason = "مرتجع سابق",
            TotalRefundAmount = 30,
            RefundedAmount = 30,
            ReturnItems = new List<ReturnItem>
            {
                new ReturnItem
                {
                    OrderItemId = orderItemId,
                    ProductId = product.Id,
                    ReturnedQuantity = 3,
                    UnitPrice = 10,
                    DiscountPercentage = 0,
                    RefundAmount = 30,
                    RestockItem = true
                }
            }
        });

        await context.SaveChangesAsync();

        var service = new ReturnProcessingService(context);
        var requestItems = new[]
        {
            new ReturnProcessingService.ReturnCreateItem
            {
                OrderItemId = orderItemId,
                ProductId = product.Id,
                Quantity = 3,
                RestockItem = true
            }
        };

        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(order.Id, ReturnTypeConstants.Partial, "مرتجع جديد", null, requestItems, "user"));
    }

    [Xunit.Fact]
    public async Task ReturnProcessingService_ApproveAsync_RestoresInventory()
    {
        using var context = CreateContext();

        var branch = new Branch { Id = 1, Name = "الفرع", Code = "BR-3" };
        var warehouse = new Warehouse { Id = 1, Name = "المستودع", BranchId = branch.Id, Branch = branch };
        var customer = new Customer { Id = 1, Name = "عميل" };
        var product = new Product { Id = 1, Name = "منتج" };

        context.Branches.Add(branch);
        context.Warehouses.Add(warehouse);
        context.Customers.Add(customer);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var order = new Order
        {
            CustomerId = customer.Id,
            OrderDate = AppClock.UtcNow,
            TotalAmount = 40,
            CustomerPayableAmount = 40,
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPrice = 20,
                    DiscountPercentage = 0
                }
            }
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var orderItemId = order.OrderItems.First().Id;

        context.ProductInventories.Add(new ProductInventory
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQuantity = 0,
            ReservedQuantity = 0,
            ReorderPoint = 0,
            LastUpdated = AppClock.UtcNow
        });

        context.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            MovementType = "إخراج",
            Quantity = 2,
            MovementDate = AppClock.UtcNow,
            Reason = "بيع للعميل",
            ReferenceNumber = "ORD-" + order.Id
        });

        await context.SaveChangesAsync();

        var service = new ReturnProcessingService(context);
        var created = await service.CreateAsync(order.Id, ReturnTypeConstants.Full, "مرتجع", null, new[]
        {
            new ReturnProcessingService.ReturnCreateItem
            {
                OrderItemId = orderItemId,
                ProductId = product.Id,
                Quantity = 2,
                RestockItem = true
            }
        }, "user");

        await service.ApproveAsync(created.Id, "admin");

        var inventory = await context.ProductInventories.SingleAsync(pi => pi.ProductId == product.Id && pi.WarehouseId == warehouse.Id);
        Xunit.Assert.Equal(2, inventory.OnHandQuantity);

        var updatedReturn = await context.Returns.FindAsync(created.Id);
        Xunit.Assert.Equal(ReturnStatusConstants.Approved, updatedReturn!.Status);

        var restockMovement = await context.StockMovements.SingleAsync(sm => sm.ReferenceNumber == $"RET-{created.Id}");
        Xunit.Assert.Equal("إدخال", restockMovement.MovementType);
        Xunit.Assert.Equal(2, restockMovement.Quantity);
    }

    [Xunit.Fact]
    public async Task ReturnProcessingService_ApproveAsync_DoesNotRestockWhenItemFlaggedFalse()
    {
        using var context = CreateContext();

        var branch = new Branch { Id = 10, Name = "فرع إضافي", Code = "BR-10" };
        var warehouse = new Warehouse { Id = 10, Name = "مستودع إضافي", BranchId = branch.Id, Branch = branch };
        var customer = new Customer { Id = 10, Name = "عميل" };
        var product = new Product { Id = 10, Name = "منتج" };

        context.Branches.Add(branch);
        context.Warehouses.Add(warehouse);
        context.Customers.Add(customer);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var order = new Order
        {
            CustomerId = customer.Id,
            OrderDate = AppClock.UtcNow,
            TotalAmount = 40,
            CustomerPayableAmount = 40,
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPrice = 20,
                    DiscountPercentage = 0
                }
            }
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var orderItemId = order.OrderItems.First().Id;

        context.ProductInventories.Add(new ProductInventory
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQuantity = 0,
            ReservedQuantity = 0,
            ReorderPoint = 0,
            LastUpdated = AppClock.UtcNow
        });

        context.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            MovementType = "إخراج",
            Quantity = 2,
            MovementDate = AppClock.UtcNow,
            Reason = "بيع للعميل",
            ReferenceNumber = "ORD-" + order.Id
        });

        await context.SaveChangesAsync();

        var service = new ReturnProcessingService(context);
        var created = await service.CreateAsync(order.Id, ReturnTypeConstants.Full, "مرتجع", null, new[]
        {
            new ReturnProcessingService.ReturnCreateItem
            {
                OrderItemId = orderItemId,
                ProductId = product.Id,
                Quantity = 2,
                RestockItem = false
            }
        }, "user");

        await service.ApproveAsync(created.Id, "admin");

        var inventory = await context.ProductInventories.SingleAsync(pi => pi.ProductId == product.Id && pi.WarehouseId == warehouse.Id);
        Xunit.Assert.Equal(0, inventory.OnHandQuantity);

        var restockMovements = await context.StockMovements
            .Where(sm => sm.ReferenceNumber == $"RET-{created.Id}")
            .ToListAsync();
        Xunit.Assert.Empty(restockMovements);
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object?> _data = new Dictionary<string, object?>();

        public IDictionary<string, object?> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object?>(_data);
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            _data = new Dictionary<string, object?>(values);
        }
    }

    private static OrdersController CreateOrdersController(ApplicationDbContext context)
    {
        var controller = new OrdersController(context, new InsuranceCalculationService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new InMemoryTempDataProvider())
        };

        return controller;
    }
}
