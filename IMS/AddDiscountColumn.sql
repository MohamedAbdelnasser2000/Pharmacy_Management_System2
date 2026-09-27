-- مزامنة عمود الخصم مع نقل البيانات إلى OrderItems
-- يفترض تشغيله على قاعدة بيانات IMS_DB

USE [IMS_DB]
GO

-- إزالة العمود من Orders عند وجوده
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'DiscountPercentage')
BEGIN
    ALTER TABLE [dbo].[Orders]
    DROP COLUMN [DiscountPercentage];
    PRINT 'تم حذف عمود DiscountPercentage من جدول Orders';
END
ELSE
BEGIN
    PRINT 'لا يوجد عمود DiscountPercentage في جدول Orders';
END
GO

-- إضافة العمود إلى OrderItems عند غيابه
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND name = 'DiscountPercentage')
BEGIN
    ALTER TABLE [dbo].[OrderItems]
    ADD [DiscountPercentage] DECIMAL(5, 2) NOT NULL DEFAULT 0;
    PRINT 'تم إضافة عمود DiscountPercentage إلى جدول OrderItems';
END
ELSE
BEGIN
    PRINT 'عمود DiscountPercentage موجود بالفعل في جدول OrderItems';
END
GO
