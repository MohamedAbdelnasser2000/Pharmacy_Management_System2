using System.Collections.Generic;

namespace IMS.ViewModels
{
    public class DashboardCard
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public string Icon { get; set; }
        public string ColorClass { get; set; }
    }

    public class DashboardViewModel
    {
        public List<DashboardCard> SummaryCards { get; set; } = new();
        public List<(string Label, int Value)> LowStockProducts { get; set; } = new();
        public List<(string ProductName, int Quantity, decimal Revenue)> TopProducts { get; set; } = new();
        public List<(int Id, string CustomerName, DateTime OrderDate, decimal TotalAmount, string Status)> RecentOrders { get; set; } = new();
        
        // Additional Statistics
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int TotalPurchaseOrders { get; set; }
        public int PendingPurchaseOrders { get; set; }
        public int ActiveCustomers { get; set; }
        public int ActiveSuppliers { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalPurchases { get; set; }
        public decimal PendingPayments { get; set; }
        public decimal SupplierPendingPayments { get; set; }
    }
}