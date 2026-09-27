using IMS.Models;

namespace IMS.Services
{
    public interface IInvoiceService
    {
        byte[] GenerateInvoicePdf(Order order, StoreSettings storeSettings);
    }
}
