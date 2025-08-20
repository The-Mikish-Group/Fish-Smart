using Members.Data;
using Members.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Members.Services
{
    public interface IBackgroundRemovalBillingService
    {
        Task<bool> TrackUsageAndCreateInvoiceIfNeeded(string userId, string serviceName, decimal apiCost);
        Task<(int freeUsed, int totalUsed, decimal totalCharges)> GetMonthlyUsageSummary(string userId);
        Task<List<BackgroundRemovalUsage>> GetUserUsageHistory(string userId, int? year = null, int? month = null);
        Task<bool> IsUserPremium(string userId);
    }

    public class BackgroundRemovalBillingService : IBackgroundRemovalBillingService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<BackgroundRemovalBillingService> _logger;
        
        // Configuration constants
        private const int FREE_MONTHLY_LIMIT = 5;
        private const decimal CHARGE_PER_IMAGE = 0.50m; // What we charge the user
        
        public BackgroundRemovalBillingService(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<BackgroundRemovalBillingService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<bool> TrackUsageAndCreateInvoiceIfNeeded(string userId, string serviceName, decimal apiCost)
        {
            try
            {
                // Check if user is premium (only premium users can use background removal)
                var isPremium = await IsUserPremium(userId);
                if (!isPremium)
                {
                    _logger.LogWarning("Non-premium user {UserId} attempted to use background removal service", userId);
                    return false;
                }

                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;
                
                // Get current month usage count
                var monthlyUsageCount = await _context.BackgroundRemovalUsage
                    .CountAsync(u => u.UserId == userId && u.UsageYear == currentYear && u.UsageMonth == currentMonth);

                var isWithinFreeLimit = monthlyUsageCount < FREE_MONTHLY_LIMIT;
                var chargeAmount = isWithinFreeLimit ? 0m : CHARGE_PER_IMAGE;

                // Create usage tracking record
                var usageRecord = new BackgroundRemovalUsage
                {
                    UserId = userId,
                    UsageDate = DateTime.UtcNow,
                    UsageMonth = currentMonth,
                    UsageYear = currentYear,
                    ServiceUsed = serviceName,
                    Cost = apiCost,
                    ChargeAmount = chargeAmount,
                    IsWithinFreeLimit = isWithinFreeLimit,
                    HasBeenInvoiced = isWithinFreeLimit, // Free usage is marked as "invoiced" since no invoice needed
                    Notes = isWithinFreeLimit ? $"Free usage ({monthlyUsageCount + 1}/{FREE_MONTHLY_LIMIT})" : "Billable overage usage"
                };

                _context.BackgroundRemovalUsage.Add(usageRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Background removal usage tracked for user {UserId}: {ServiceName}, Cost: {Cost}, Charge: {Charge}, Free: {IsFree}", 
                    userId, serviceName, apiCost, chargeAmount, isWithinFreeLimit);

                // Create invoice if this is a billable usage (over free limit)
                if (!isWithinFreeLimit)
                {
                    var invoiceCreated = await CreateUsageInvoice(userId, usageRecord);
                    if (invoiceCreated)
                    {
                        _logger.LogInformation("Invoice created for background removal overage usage by user {UserId}", userId);
                    }
                    else
                    {
                        _logger.LogError("Failed to create invoice for background removal overage usage by user {UserId}", userId);
                    }
                    return invoiceCreated;
                }

                return true; // Success for free usage
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking background removal usage for user {UserId}", userId);
                return false;
            }
        }

        private async Task<bool> CreateUsageInvoice(string userId, BackgroundRemovalUsage usageRecord)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError("User {UserId} not found when creating background removal invoice", userId);
                    return false;
                }

                // Create invoice using the same pattern as your existing AddInvoice system
                var invoice = new Invoice
                {
                    UserID = userId,
                    InvoiceDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(30), // 30-day payment terms
                    Description = $"Background Removal Service - {usageRecord.ServiceUsed} ({usageRecord.UsageDate:MMM dd, yyyy})",
                    AmountDue = usageRecord.ChargeAmount,
                    AmountPaid = 0m,
                    Status = InvoiceStatus.Due,
                    Type = InvoiceType.BackgroundRemoval,
                    DateCreated = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync(); // Save to get the InvoiceID

                // Update usage record with invoice reference
                usageRecord.InvoiceId = invoice.InvoiceID;
                usageRecord.HasBeenInvoiced = true;
                usageRecord.DateInvoiced = DateTime.UtcNow;
                
                _context.BackgroundRemovalUsage.Update(usageRecord);

                // Apply available credits (following your existing credit application pattern)
                await ApplyAvailableCreditsToInvoice(invoice, userId);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Created background removal invoice {InvoiceId} for user {UserId}, Amount: {Amount}", 
                    invoice.InvoiceID, userId, invoice.AmountDue);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating background removal invoice for user {UserId}", userId);
                return false;
            }
        }

        private async Task ApplyAvailableCreditsToInvoice(Invoice invoice, string userId)
        {
            // This mirrors the credit application logic from your AddInvoice system
            var availableCredits = await _context.UserCredits
                .Where(uc => uc.UserID == userId && !uc.IsApplied && uc.Amount > 0)
                .OrderBy(uc => uc.CreditDate)
                .ToListAsync();

            var remainingAmountDue = invoice.AmountDue;

            foreach (var credit in availableCredits)
            {
                if (remainingAmountDue <= 0) break;

                var amountToApply = Math.Min(remainingAmountDue, credit.Amount);
                if (amountToApply <= 0) continue;

                // Create CreditApplication record
                var creditApplication = new CreditApplication
                {
                    UserCreditID = credit.UserCreditID,
                    InvoiceID = invoice.InvoiceID,
                    AmountApplied = amountToApply,
                    ApplicationDate = DateTime.UtcNow,
                    Notes = $"Auto-applied to Background Removal charge (INV-{invoice.InvoiceID:D5}). Original Credit: {credit.Reason}"
                };

                _context.CreditApplications.Add(creditApplication);

                // Update credit
                credit.Amount -= amountToApply;
                credit.LastUpdated = DateTime.UtcNow;
                
                if (credit.Amount <= 0)
                {
                    credit.IsApplied = true;
                    credit.Amount = 0;
                    credit.AppliedDate = DateTime.UtcNow;
                }

                _context.UserCredits.Update(credit);

                // Update invoice
                invoice.AmountPaid += amountToApply;
                remainingAmountDue -= amountToApply;
            }

            // Update invoice status based on payments
            if (invoice.AmountPaid >= invoice.AmountDue)
            {
                invoice.Status = InvoiceStatus.Paid;
                invoice.AmountPaid = invoice.AmountDue;
            }

            invoice.LastUpdated = DateTime.UtcNow;
            _context.Invoices.Update(invoice);
        }

        public async Task<(int freeUsed, int totalUsed, decimal totalCharges)> GetMonthlyUsageSummary(string userId)
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var monthlyUsage = await _context.BackgroundRemovalUsage
                .Where(u => u.UserId == userId && u.UsageYear == currentYear && u.UsageMonth == currentMonth)
                .ToListAsync();

            var freeUsed = monthlyUsage.Count(u => u.IsWithinFreeLimit);
            var totalUsed = monthlyUsage.Count;
            var totalCharges = monthlyUsage.Where(u => !u.IsWithinFreeLimit).Sum(u => u.ChargeAmount);

            return (freeUsed, totalUsed, totalCharges);
        }

        public async Task<List<BackgroundRemovalUsage>> GetUserUsageHistory(string userId, int? year = null, int? month = null)
        {
            var query = _context.BackgroundRemovalUsage
                .Include(u => u.Invoice)
                .Where(u => u.UserId == userId);

            if (year.HasValue)
                query = query.Where(u => u.UsageYear == year);

            if (month.HasValue)
                query = query.Where(u => u.UsageMonth == month);

            return await query
                .OrderByDescending(u => u.UsageDate)
                .ToListAsync();
        }

        public async Task<bool> IsUserPremium(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            // Check if user has Premium role
            return await _userManager.IsInRoleAsync(user, "Premium");
        }
    }
}