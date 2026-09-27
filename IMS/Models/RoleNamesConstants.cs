namespace IMS.Models
{
    public static class RoleNamesConstants
    {
        public const string Admin = "Admin";
        public const string Accountant = "Accountant";
        public const string WarehouseStaff = "WarehouseStaff";
        public const string Pharmacist = "Pharmacist";
        public const string Cashier = "Cashier";
        public const string DataEntry = "DataEntry";
    }

    public static class ReturnStatusConstants
    {
        public const string Pending = "معلق";
        public const string Approved = "مقبول";
        public const string Rejected = "مرفوض";
        public const string Completed = "مكتمل";
    }

    public static class ReturnTypeConstants
    {
        public const string Full = "استرجاع كامل";
        public const string Partial = "استرجاع جزئي";
    }
}
