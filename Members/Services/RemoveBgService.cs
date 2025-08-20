using System.Text;
using System.Text.Json;

namespace Members.Services
{
    public class RemoveBgService : IBackgroundRemovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RemoveBgService> _logger;
        private readonly string _apiKey;

        public string ServiceName => "Remove.bg";
        public decimal CostPerImage => 0.20m; // $0.20 per image
        public int FreeCreditsPerMonth => 50; // 50 free images per month

        public RemoveBgService(HttpClient httpClient, IConfiguration configuration, ILogger<RemoveBgService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["BackgroundRemoval:RemoveBg:ApiKey"] ?? string.Empty;
            
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
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
                        Message = "⚠️ Remove.bg API key not configured. Please add your API key to continue using background removal." 
                    };
                }

                // Check quota first to provide better error messages
                var quotaInfo = await GetQuotaInfoAsync();
                if (quotaInfo.CreditsRemaining <= 0 && !quotaInfo.HasUnlimitedCredits)
                {
                    return new BackgroundRemovalResult
                    {
                        Success = false,
                        Message = "❌ Remove.bg credits exhausted! You've used all 50 free monthly credits. Visit Remove.bg to purchase more credits or wait until next month for free credits to reset."
                    };
                }

                using var content = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("image/jpeg");
                content.Add(imageContent, "image_file", fileName);
                content.Add(new StringContent("auto"), "size");

                var response = await _httpClient.PostAsync("https://api.remove.bg/v1.0/removebg", content);
                
                result.ProcessingTime = DateTime.UtcNow - startTime;

                if (response.IsSuccessStatusCode)
                {
                    result.ProcessedImageBytes = await response.Content.ReadAsByteArrayAsync();
                    result.Success = true;
                    result.Message = "Background removed successfully";
                    result.CostIncurred = CostPerImage;
                    
                    // Add metadata
                    result.Metadata["service"] = ServiceName;
                    result.Metadata["api_version"] = "v1.0";
                    result.Metadata["original_size"] = imageBytes.Length.ToString();
                    result.Metadata["processed_size"] = result.ProcessedImageBytes.Length.ToString();
                    
                    // Get quota info from headers if available
                    if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
                        result.Metadata["credits_remaining"] = remaining.FirstOrDefault() ?? "unknown";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Unauthorized => "❌ Invalid Remove.bg API key. Please check your API configuration.",
                        System.Net.HttpStatusCode.PaymentRequired => "💳 Remove.bg credits exhausted! Please purchase more credits or wait for monthly reset.",
                        System.Net.HttpStatusCode.TooManyRequests => "⏰ Remove.bg rate limit exceeded. Please wait before trying again.",
                        System.Net.HttpStatusCode.BadRequest => "🖼️ Invalid image format. Please use JPG or PNG files under 12MB.",
                        _ => $"❌ Remove.bg API error: {response.StatusCode} - {errorContent}"
                    };
                    
                    result.Success = false;
                    result.Message = errorMessage;
                    
                    _logger.LogError("Remove.bg API failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error calling Remove.bg API: {ex.Message}";
                result.ProcessingTime = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Exception in Remove.bg service");
            }

            return result;
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                    return false;

                var response = await _httpClient.GetAsync("https://api.remove.bg/v1.0/account");
                return response.IsSuccessStatusCode;
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
                var response = await _httpClient.GetAsync("https://api.remove.bg/v1.0/account");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var accountInfo = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    return new ServiceQuotaInfo
                    {
                        CreditsRemaining = accountInfo.TryGetProperty("api", out var api) && 
                                         api.TryGetProperty("free_calls", out var freeCalls) 
                                         ? freeCalls.GetInt32() : 0,
                        HasUnlimitedCredits = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Remove.bg quota info");
            }

            return new ServiceQuotaInfo { CreditsRemaining = 0 };
        }
    }
}