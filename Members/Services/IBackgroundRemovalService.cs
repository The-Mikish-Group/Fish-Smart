namespace Members.Services
{
    public interface IBackgroundRemovalService
    {
        string ServiceName { get; }
        decimal CostPerImage { get; }
        int FreeCreditsPerMonth { get; }
        Task<BackgroundRemovalResult> RemoveBackgroundAsync(string imagePath);
        Task<BackgroundRemovalResult> RemoveBackgroundFromBytesAsync(byte[] imageBytes, string fileName);
        Task<bool> IsServiceAvailableAsync();
        Task<ServiceQuotaInfo> GetQuotaInfoAsync();
    }

    public class BackgroundRemovalResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public byte[]? ProcessedImageBytes { get; set; }
        public string? ProcessedImagePath { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public decimal CostIncurred { get; set; }
        public int QualityScore { get; set; } // 1-10 subjective rating
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class ServiceQuotaInfo
    {
        public int CreditsRemaining { get; set; }
        public int CreditsUsedThisMonth { get; set; }
        public DateTime? ResetDate { get; set; }
        public bool HasUnlimitedCredits { get; set; }
    }

    public class BackgroundRemovalTestResult
    {
        public string ServiceName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
        public decimal Cost { get; set; }
        public int QualityRating { get; set; }
        public string ResultImagePath { get; set; } = string.Empty;
        public Dictionary<string, string> TechnicalDetails { get; set; } = new();
    }
}