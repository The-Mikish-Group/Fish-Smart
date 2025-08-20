using System.Text;
using System.Text.Json;

namespace Members.Services
{
    public class EraseBgService : IBackgroundRemovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EraseBgService> _logger;
        private readonly string _apiKey;

        public string ServiceName => "Erase.bg";
        public decimal CostPerImage => 0.10m; // Much cheaper than Remove.bg
        public int FreeCreditsPerMonth => 25; // 25 free credits for new users

        public EraseBgService(HttpClient httpClient, IConfiguration configuration, ILogger<EraseBgService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["BackgroundRemoval:EraseBg:ApiKey"] ?? string.Empty;
        }

        public async Task<BackgroundRemovalResult> RemoveBackgroundAsync(string imagePath)
        {
            if (!File.Exists(imagePath))
                return new BackgroundRemovalResult { Success = false, Message = "Image file not found" };

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            return await RemoveBackgroundFromBytesAsync(imageBytes, Path.GetFileName(imagePath));
        }

        public async Task<BackgroundRemovalResult> RemoveBackgroundFromBytesAsync(byte[] imageBytes, string fileName)
        {
            var startTime = DateTime.UtcNow;
            var result = new BackgroundRemovalResult();

            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return new BackgroundRemovalResult 
                    { 
                        Success = false, 
                        Message = "⚠️ Erase.bg API key not configured. Please add your API key to continue using background removal." 
                    };
                }

                // Check quota first to provide better error messages
                var quotaInfo = await GetQuotaInfoAsync();
                if (quotaInfo.CreditsRemaining <= 0 && !quotaInfo.HasUnlimitedCredits)
                {
                    return new BackgroundRemovalResult
                    {
                        Success = false,
                        Message = "❌ Background removal credits exhausted! Visit Erase.bg to purchase more credits or try again next month for free credits."
                    };
                }

                using var content = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("image/jpeg");
                content.Add(imageContent, "image_file", fileName);
                
                // Add API key to request
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.PostAsync("https://clipdrop-api.co/remove-background/v1", content);
                
                result.ProcessingTime = DateTime.UtcNow - startTime;

                if (response.IsSuccessStatusCode)
                {
                    result.ProcessedImageBytes = await response.Content.ReadAsByteArrayAsync();
                    result.Success = true;
                    result.Message = "✅ Background removed successfully using Erase.bg";
                    result.CostIncurred = CostPerImage;
                    
                    // Add metadata
                    result.Metadata["service"] = ServiceName;
                    result.Metadata["cost_per_image"] = CostPerImage.ToString("C");
                    result.Metadata["original_size"] = imageBytes.Length.ToString();
                    result.Metadata["processed_size"] = result.ProcessedImageBytes.Length.ToString();
                    result.Metadata["credits_used"] = "1";
                    
                    _logger.LogInformation("Erase.bg background removal successful. Size: {OriginalSize} -> {ProcessedSize}", 
                        imageBytes.Length, result.ProcessedImageBytes.Length);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Unauthorized => "❌ Invalid API key. Please check your Erase.bg API configuration.",
                        System.Net.HttpStatusCode.PaymentRequired => "💳 Background removal credits exhausted! Please purchase more credits from Erase.bg.",
                        System.Net.HttpStatusCode.TooManyRequests => "⏰ Rate limit exceeded. Please wait before trying again.",
                        System.Net.HttpStatusCode.BadRequest => "🖼️ Invalid image format. Please use JPG or PNG files.",
                        _ => $"❌ Erase.bg API error: {response.StatusCode} - {errorContent}"
                    };
                    
                    result.Success = false;
                    result.Message = errorMessage;
                    
                    _logger.LogError("Erase.bg API failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"❌ Error calling Erase.bg API: {ex.Message}";
                result.ProcessingTime = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Exception in Erase.bg service");
            }

            return result;
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                    return false;

                // Test with a simple quota check
                var quotaInfo = await GetQuotaInfoAsync();
                return quotaInfo != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ServiceQuotaInfo> GetQuotaInfoAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                    return new ServiceQuotaInfo { CreditsRemaining = 0 };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                
                // Note: Adjust this endpoint based on Erase.bg's actual API documentation
                var response = await _httpClient.GetAsync("https://api.erase.bg/v1/account");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var accountInfo = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    return new ServiceQuotaInfo
                    {
                        CreditsRemaining = accountInfo.TryGetProperty("credits_remaining", out var credits) 
                                         ? credits.GetInt32() : FreeCreditsPerMonth,
                        HasUnlimitedCredits = accountInfo.TryGetProperty("unlimited", out var unlimited) && unlimited.GetBoolean()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Erase.bg quota info");
            }

            return new ServiceQuotaInfo { CreditsRemaining = FreeCreditsPerMonth };
        }
    }
}