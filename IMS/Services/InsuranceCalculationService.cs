using IMS.Data;
using IMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.Services
{
    public class InsuranceCalculationService : IInsuranceService
    {
        private readonly ApplicationDbContext _context;

        public InsuranceCalculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// حساب التغطية التأمينية للطلب أو الوصفة
        /// </summary>
        /// <param name="policyId">معرف البوليصة التأمينية</param>
        /// <param name="totalAmount">المبلغ الإجمالي</param>
        /// <returns>نتيجة حساب التأمين</returns>
        public async Task<InsuranceCalculationResult> CalculateInsuranceCoverageAsync(int? policyId, decimal totalAmount)
        {
            var result = new InsuranceCalculationResult
            {
                TotalAmount = totalAmount,
                InsuranceCoverageAmount = 0,
                CustomerPayableAmount = totalAmount,
                AppliedCoveragePercentage = null,
                IsValid = false
            };

            if (!policyId.HasValue || totalAmount <= 0)
            {
                return result;
            }

            // جلب البوليصة مع شركة التأمين
            var policy = await _context.InsurancePolicies
                .Include(p => p.Insurance)
                .FirstOrDefaultAsync(p => p.Id == policyId.Value);

            if (policy == null)
            {
                result.ErrorMessage = "البوليصة التأمينية غير موجودة";
                return result;
            }

            // التحقق من صلاحية البوليصة
            if (!policy.IsValid)
            {
                result.ErrorMessage = "البوليصة التأمينية غير صالحة أو منتهية الصلاحية";
                return result;
            }

            // التحقق من المبلغ المتبقي
            var remainingAmount = policy.RemainingAmount;
            if (remainingAmount <= 0)
            {
                result.ErrorMessage = "تم استنفاد الحد الأقصى للتغطية التأمينية";
                return result;
            }

            // حساب مبلغ التغطية
            var coveragePercentage = policy.CoveragePercentage;
            var calculatedCoverage = totalAmount * (coveragePercentage / 100);

            // التأكد من عدم تجاوز الحد الأقصى المتبقي
            var actualCoverage = Math.Min(calculatedCoverage, remainingAmount);

            result.InsuranceCoverageAmount = Math.Round(actualCoverage, 2);
            result.CustomerPayableAmount = Math.Round(totalAmount - result.InsuranceCoverageAmount, 2);
            result.AppliedCoveragePercentage = coveragePercentage;
            result.IsValid = true;
            result.PolicyNumber = policy.PolicyNumber;
            result.InsuranceName = policy.Insurance?.Name;

            return result;
        }

        /// <summary>
        /// تحديث المبلغ المستخدم في البوليصة
        /// </summary>
        public async Task<bool> UpdatePolicyUsedAmountAsync(int policyId, decimal amountToAdd)
        {
            var policy = await _context.InsurancePolicies.FindAsync(policyId);
            if (policy == null)
            {
                return false;
            }

            policy.UsedAmount += amountToAdd;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// إرجاع المبلغ المستخدم في حالة الإلغاء
        /// </summary>
        public async Task<bool> RefundPolicyUsedAmountAsync(int policyId, decimal amountToRefund)
        {
            var policy = await _context.InsurancePolicies.FindAsync(policyId);
            if (policy == null)
            {
                return false;
            }

            policy.UsedAmount = Math.Max(0, policy.UsedAmount - amountToRefund);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// الحصول على البوليصات الصالحة للعميل
        /// </summary>
        public async Task<List<InsurancePolicy>> GetValidPoliciesForCustomerAsync(int customerId)
        {
            return await _context.InsurancePolicies
                .Include(p => p.Insurance)
                .Where(p => p.CustomerId == customerId && 
                           p.IsActive && 
                           p.StartDate <= AppClock.UtcNow && 
                           p.EndDate >= AppClock.UtcNow)
                .OrderByDescending(p => p.CoveragePercentage)
                .ToListAsync();
        }
    }

    /// <summary>
    /// نتيجة حساب التغطية التأمينية
    /// </summary>
    public class InsuranceCalculationResult
    {
        public decimal TotalAmount { get; set; }
        public decimal InsuranceCoverageAmount { get; set; }
        public decimal CustomerPayableAmount { get; set; }
        public decimal? AppliedCoveragePercentage { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? PolicyNumber { get; set; }
        public string? InsuranceName { get; set; }
    }
}
