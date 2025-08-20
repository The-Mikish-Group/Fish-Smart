using System.Text.Json;

namespace Members.Services
{
    public class ClipdropService : IBackgroundRemovalService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ClipdropService> _logger;
        private readonly string _apiKey;

        public string ServiceName => "Clipdrop";
        public decimal CostPerImage => 0.09m; // $0.09 per image
        public int FreeCreditsPerMonth => 100; // 100 free credits initially

        public ClipdropService(HttpClient httpClient, IConfiguration configuration, ILogger<ClipdropService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["BackgroundRemoval:Clipdrop:ApiKey"] ?? string.Empty;
            
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
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
                        Message = "Clipdrop API key not configured" 
                    };
                }

                using var content = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("image/jpeg");
                content.Add(imageContent, "image_file", fileName);

                var response = await _httpClient.PostAsync("https://clipdrop-api.co/remove-background/v1", content);
                
                result.ProcessingTime = DateTime.UtcNow - startTime;

                if (response.IsSuccessStatusCode)
                {
                    result.ProcessedImageBytes = await response.Content.ReadAsByteArrayAsync();
                    result.Success = true;
                    result.Message = "Background removed successfully";
                    result.CostIncurred = CostPerImage;
                    
                    // Add metadata
                    result.Metadata["service"] = ServiceName;
                    result.Metadata["api_version"] = "v1";
                    result.Metadata["original_size"] = imageBytes.Length.ToString();
                    result.Metadata["processed_size"] = result.ProcessedImageBytes.Length.ToString();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    result.Success = false;
                    result.Message = $"Clipdrop API error: {response.StatusCode} - {errorContent}";
                    
                    _logger.LogError("Clipdrop API failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error calling Clipdrop API: {ex.Message}";
                result.ProcessingTime = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Exception in Clipdrop service");
            }

            return result;
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                    return false;

                // Clipdrop doesn't have a health check endpoint, so we'll assume it's available if we have a key
                await Task.Delay(1); // Make it actually async
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ServiceQuotaInfo> GetQuotaInfoAsync()
        {
            // Clipdrop doesn't provide quota information via API
            // You'd need to track usage manually or check their dashboard
            await Task.Delay(1); // Make it actually async
            return new ServiceQuotaInfo 
            { 
                CreditsRemaining = -1, // Unknown
                HasUnlimitedCredits = false 
            };
        }
    }
}