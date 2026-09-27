using BarcodeLib;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IMS.Services
{
    public class BarcodeService
    {
        /// <summary>
        /// توليد باركود فريد بناءً على معرف المنتج والوقت الحالي
        /// </summary>
        public string GenerateUniqueBarcode(int productId)
        {
            // توليد باركود بتنسيق: PRD + معرف المنتج (5 أرقام) + الوقت (6 أرقام)
            var timestamp = AppClock.UtcNow.ToString("HHmmss");
            var barcode = $"PRD{productId:D5}{timestamp}";
            return barcode;
        }

        /// <summary>
        /// توليد صورة باركود
        /// </summary>
        public byte[] GenerateBarcodeImage(string barcodeValue, int width = 300, int height = 100)
        {
            try
            {
                var barcode = new BarcodeLib.Barcode();
                barcode.IncludeLabel = true;
                barcode.Alignment = BarcodeLib.AlignmentPositions.CENTER;
                barcode.LabelPosition = BarcodeLib.LabelPositions.BOTTOMCENTER;
                
                // استخدام CODE128 لأنه يدعم الأرقام والحروف
                var image = barcode.Encode(BarcodeLib.TYPE.CODE128, barcodeValue, width, height);
                
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"خطأ في توليد الباركود: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// إنشاء ملف PDF يحتوي على باركودات متعددة للطباعة
        /// </summary>
        public byte[] GenerateBarcodePDF(string productName, string barcodeValue, int quantity, string categoryName = null)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    // إنشاء مستند PDF بحجم A4
                    var document = new Document(PageSize.A4, 20, 20, 20, 20);
                    var writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    // إضافة عنوان
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                    var title = new Paragraph($"Barcodes for: {productName}", titleFont)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 20
                    };
                    document.Add(title);

                    // إضافة معلومات
                    var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                    var categoryInfo = !string.IsNullOrEmpty(categoryName) ? $" | Category: {categoryName}" : "";
                    var info = new Paragraph($"Barcode: {barcodeValue} | Quantity: {quantity}{categoryInfo} | Date: {AppClock.UtcNow:yyyy-MM-dd HH:mm}", infoFont)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 20
                    };
                    document.Add(info);

                    // إنشاء جدول للباركودات (3 أعمدة)
                    var table = new PdfPTable(3)
                    {
                        WidthPercentage = 100,
                        SpacingBefore = 10
                    };

                    // توليد الباركودات
                    for (int i = 0; i < quantity; i++)
                    {
                        var cell = CreateBarcodeCell(productName, barcodeValue, i + 1, categoryName);
                        table.AddCell(cell);
                    }

                    // ملء الصفوف الفارغة إذا لزم الأمر
                    int remainder = quantity % 3;
                    if (remainder != 0)
                    {
                        for (int i = 0; i < (3 - remainder); i++)
                        {
                            table.AddCell(new PdfPCell(new Phrase("")) { Border = Rectangle.NO_BORDER });
                        }
                    }

                    document.Add(table);
                    document.Close();
                    writer.Close();

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"خطأ في إنشاء ملف PDF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// إنشاء خلية تحتوي على باركود واحد
        /// </summary>
        private PdfPCell CreateBarcodeCell(string productName, string barcodeValue, int serialNumber, string categoryName)
        {
            var cellTable = new PdfPTable(1)
            {
                WidthPercentage = 100
            };

            // اسم المنتج
            var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var namePhrase = new Phrase(TruncateString(productName, 30), nameFont);
            var nameCell = new PdfPCell(namePhrase)
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                Border = Rectangle.NO_BORDER,
                PaddingBottom = 3
            };
            cellTable.AddCell(nameCell);

            // صورة الباركود
            var barcodeImage = GenerateBarcodeImage(barcodeValue, 250, 80);
            var image = iTextSharp.text.Image.GetInstance(barcodeImage);
            image.ScaleToFit(180f, 60f);
            var imageCell = new PdfPCell(image)
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                Border = Rectangle.NO_BORDER,
                PaddingBottom = 5
            };
            cellTable.AddCell(imageCell);

            // الفئة (إذا كانت متوفرة)
            if (!string.IsNullOrEmpty(categoryName))
            {
                var categoryFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
                var categoryPhrase = new Phrase($"[{categoryName}]", categoryFont);
                var categoryCell = new PdfPCell(categoryPhrase)
                {
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Border = Rectangle.NO_BORDER,
                    PaddingBottom = 3
                };
                cellTable.AddCell(categoryCell);
            }

            // رقم تسلسلي
            var serialFont = FontFactory.GetFont(FontFactory.HELVETICA, 7);
            var serialPhrase = new Phrase($"#{serialNumber}", serialFont);
            var serialCell = new PdfPCell(serialPhrase)
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                Border = Rectangle.NO_BORDER
            };
            cellTable.AddCell(serialCell);

            // الخلية الرئيسية
            var mainCell = new PdfPCell(cellTable)
            {
                Padding = 10,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };

            return mainCell;
        }

        /// <summary>
        /// اختصار النص إذا كان طويلاً
        /// </summary>
        private string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// توليد باركودات متعددة بأرقام تسلسلية
        /// </summary>
        public List<string> GenerateMultipleBarcodes(string baseBarcode, int quantity)
        {
            var barcodes = new List<string>();
            
            // استخدام نفس الباركود لجميع وحدات المنتج
            // يمكن تمييزها بالرقم التسلسلي في PDF
            for (int i = 0; i < quantity; i++)
            {
                barcodes.Add(baseBarcode);
            }
            
            return barcodes;
        }
    }
}
