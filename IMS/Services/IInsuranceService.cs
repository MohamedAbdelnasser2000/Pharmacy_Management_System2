using IMS.Models;

namespace IMS.Services
{
    public interface IInsuranceService
    {
        Task<InsuranceCalculationResult> CalculateInsuranceCoverageAsync(int? policyId, decimal totalAmount);
        Task<bool> UpdatePolicyUsedAmountAsync(int policyId, decimal amountToAdd);
        Task<bool> RefundPolicyUsedAmountAsync(int policyId, decimal amountToRefund);
        Task<List<InsurancePolicy>> GetValidPoliciesForCustomerAsync(int customerId);
    }
}
