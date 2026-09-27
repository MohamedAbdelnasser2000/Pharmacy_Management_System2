using IMS.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace IMS.Services
{
    public class InvoiceService : IInvoiceService
    {
        static InvoiceService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInvoicePdf(Order order, StoreSettings storeSettings)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));
                    page.ContentFromRightToLeft(); // RTL for Arabic

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(storeSettings.StoreName).FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"{storeSettings.Address}").FontSize(9);
                            col.Item().Text($"هاتف: {storeSettings.PhoneNumber}").FontSize(9);
                        });

                        row.RelativeItem().AlignLeft().Column(col =>
                        {
                            col.Item().Text("فاتورة مبيعات").FontSize(18).SemiBold();
                            col.Item().Text($"رقم الفاتورة: #{order.Id}").FontSize(10);
                            col.Item().Text($"التاريخ: {order.OrderDate.ToString("yyyy/MM/dd HH:mm")}").FontSize(10);
                        });
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        
                        col.Item().PaddingVertical(5).Row(row => {
                            row.RelativeItem().Text(t => {
                                t.Span("العميل: ").SemiBold();
                                t.Span(order.Customer?.Name ?? "عميل نقدي");
                            });
                            if (!string.IsNullOrEmpty(order.Customer?.PhoneNumber)) {
                                row.RelativeItem().AlignLeft().Text(t => {
                                    t.Span("الهاتف: ").SemiBold();
                                    t.Span(order.Customer.PhoneNumber);
                                });
                            }
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn();
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(60);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("#");
                                header.Cell().Element(CellStyle).Text("الصنف");
                                header.Cell().Element(CellStyle).Text("الكمية");
                                header.Cell().Element(CellStyle).Text("السعر");
                                header.Cell().Element(CellStyle).Text("الإجمالي");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                }
                            });

                            int i = 1;
                            foreach (var item in order.OrderItems)
                            {
                                table.Cell().Element(CellStyle).Text(i++.ToString());
                                table.Cell().Element(CellStyle).Text(item.Product?.Name ?? "غير معروف");
                                table.Cell().Element(CellStyle).Text(item.Quantity.ToString("N0"));
                                table.Cell().Element(CellStyle).Text(item.UnitPrice.ToString("N2"));
                                table.Cell().Element(CellStyle).Text((item.Quantity * item.UnitPrice).ToString("N2"));

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                }
                            }
                        });

                        col.Item().AlignLeft().PaddingTop(10).Column(c => {
                            decimal subtotal = order.OrderItems.Sum(oi => (oi.Quantity * oi.UnitPrice) - (oi.Quantity * oi.UnitPrice * oi.DiscountPercentage / 100));
                            
                            if (order.DiscountAmount > 0)
                            {
                                c.Item().Row(r => {
                                    r.RelativeItem().Text("الإجمالي قبل الكوبون:");
                                    r.ConstantItem(80).Text(subtotal.ToString("N2"));
                                });
                                c.Item().Row(r => {
                                    r.RelativeItem().Text($"خصم الكوبون {(order.DiscountCode != null ? $"({order.DiscountCode.Code})" : "")}:");
                                    r.ConstantItem(80).Text($"-{order.DiscountAmount.ToString("N2")}").FontColor(Colors.Red.Medium);
                                });
                            }

                            c.Item().Row(r => {
                                r.RelativeItem().Text("الإجمالي النهائي:");
                                r.ConstantItem(80).Text(order.TotalAmount.ToString("N2")).SemiBold();
                            });
                            
                            if (order.InsuranceCoverageAmount > 0) {
                                c.Item().Row(r => {
                                    r.RelativeItem().Text("تغطية التأمين:");
                                    r.ConstantItem(80).Text(order.InsuranceCoverageAmount.ToString("N2")).FontColor(Colors.Green.Medium);
                                });
                                c.Item().Row(r => {
                                    r.RelativeItem().Text("المطلوب من العميل:");
                                    r.ConstantItem(80).Text(order.CustomerPayableAmount.ToString("N2")).FontSize(12).SemiBold();
                                });
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("شكراً لتعاملكم معنا. نتمنى لكم الشفاء العاجل.");
                        t.EmptyLine();
                        t.Span("صفحة ");
                        t.CurrentPageNumber();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
