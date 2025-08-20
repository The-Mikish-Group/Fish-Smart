using Microsoft.AspNetCore.Identity;

namespace Members.Services
{
    public interface IProductionBackgroundRemovalService
    {
        Task<(bool success, byte[]? processedImage, string message, decimal cost)> ProcessBackgroundRemovalAsync(string userId, string imagePath);
        Task<(int freeRemaining, int totalUsed, decimal monthlyCharges)> GetUserUsageSummary(string userId);
        Task<bool> CanUserAccessService(string userId);
    }

    public class ProductionBackgroundRemovalService : IProductionBackgroundRemovalService
    {
        private readonly IBackgroundRemovalBillingService _billingService;
        private readonly RemoveBgService _removeBgService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ProductionBackgroundRemovalService> _logger;

        // Service configuration
        private const int FREE_MONTHLY_LIMIT = 5;
        private const decimal REMOVE_BG_COST = 0.20m; // Actual API cost

        public ProductionBackgroundRemovalService(
            IBackgroundRemovalBillingService billingService,
            RemoveBgService removeBgService,
            UserManager<IdentityUser> userManager,
            ILogger<ProductionBackgroundRemovalService> logger)
        {
            _billingService = billingService;
            _removeBgService = removeBgService;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<(bool success, byte[]? processedImage, string message, decimal cost)> ProcessBackgroundRemovalAsync(string userId, string imagePath)
        {
            try
            {
                // Check if user can access the service
                if (!await CanUserAccessService(userId))
                {
                    return (false, null, "Background removal service is only available to Premium members.", 0m);
                }

                // Get current usage to inform user
                var (freeUsed, totalUsed, _) = await _billingService.GetMonthlyUsageSummary(userId);
                var isOverLimit = totalUsed >= FREE_MONTHLY_LIMIT;

                // Log the attempt
                _logger.LogInformation("User {UserId} attempting background removal. Current usage: {TotalUsed}, Free limit: {FreeLimit}", 
                    userId, totalUsed, FREE_MONTHLY_LIMIT);

                // Process the image using Remove.bg
                var result = await _removeBgService.RemoveBackgroundAsync(imagePath);
                
                if (!result.Success)
                {
                    _logger.LogWarning("Remove.bg failed for user {UserId}: {Message}", userId, result.Message);
                    return (false, null, $"Background removal failed: {result.Message}", 0m);
                }

                // Track usage and create invoice if needed
                var trackingSuccess = await _billingService.TrackUsageAndCreateInvoiceIfNeeded(userId, "Remove.bg", REMOVE_BG_COST);
                
                if (!trackingSuccess)
                {
                    _logger.LogError("Failed to track usage for user {UserId} after successful background removal", userId);
                    // Still return the processed image but log the billing error
                }

                // Determine the message to return
                string message;
                if (isOverLimit)
                {
                    message = $"Background removed successfully! You've exceeded your {FREE_MONTHLY_LIMIT} free monthly images. This usage has been added to your account for billing.";
                }
                else
                {
                    var remainingFree = FREE_MONTHLY_LIMIT - (totalUsed + 1);
                    message = $"Background removed successfully! You have {remainingFree} free background removals remaining this month.";
                }

                _logger.LogInformation("Background removal successful for user {UserId}. Free remaining: {FreeRemaining}", 
                    userId, Math.Max(0, FREE_MONTHLY_LIMIT - (totalUsed + 1)));

                return (true, result.ProcessedImageBytes, message, isOverLimit ? 0.50m : 0m);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing background removal for user {UserId}", userId);
                return (false, null, "An error occurred while processing your image. Please try again.", 0m);
            }
        }

        public async Task<(int freeRemaining, int totalUsed, decimal monthlyCharges)> GetUserUsageSummary(string userId)
        {
            var (freeUsed, totalUsed, monthlyCharges) = await _billingService.GetMonthlyUsageSummary(userId);
            var freeRemaining = Math.Max(0, FREE_MONTHLY_LIMIT - totalUsed);
            
            return (freeRemaining, totalUsed, monthlyCharges);
        }

        public async Task<bool> CanUserAccessService(string userId)
        {
            return await _billingService.IsUserPremium(userId);
        }
    }
}