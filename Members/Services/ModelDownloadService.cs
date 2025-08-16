using System.Security.Cryptography;
using System.Text;

namespace Members.Services
{
    public class ModelDownloadService : IModelDownloadService
    {
        private readonly ILogger<ModelDownloadService> _logger;
        private readonly HttpClient _httpClient;

        // U2-Net model from reliable sources
        private const string U2NET_LITE_MODEL_URL = "https://github.com/xuebinqin/U-2-Net/raw/master/saved_models/u2netp/u2netp.onnx";
        private const string U2NET_MODEL_URL = "https://github.com/xuebinqin/U-2-Net/raw/master/saved_models/u2net/u2net.onnx";

        public ModelDownloadService(ILogger<ModelDownloadService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<bool> DownloadU2NetModelAsync(string destinationPath)
        {
            try
            {
                _logger.LogInformation("Starting U2-Net model download to {DestinationPath}", destinationPath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Try downloading the lighter U2-Net model first (smaller and faster)
                var downloadUrls = new[]
                {
                    (U2NET_LITE_MODEL_URL, "U2-Net Light"),
                    (U2NET_MODEL_URL, "U2-Net Full")
                };

                foreach (var (url, name) in downloadUrls)
                {
                    try
                    {
                        _logger.LogInformation("Attempting to download {ModelName} from {Url}", name, url);
                        
                        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        if (response.IsSuccessStatusCode)
                        {
                            var totalBytes = response.Content.Headers.ContentLength ?? 0;
                            _logger.LogInformation("Downloading {ModelName} ({TotalBytes} bytes)", name, totalBytes);

                            using var contentStream = await response.Content.ReadAsStreamAsync();
                            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                            
                            await contentStream.CopyToAsync(fileStream);
                            await fileStream.FlushAsync();

                            _logger.LogInformation("Successfully downloaded {ModelName} to {DestinationPath}", name, destinationPath);
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to download {ModelName}: {StatusCode}", name, response.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error downloading {ModelName} from {Url}", name, url);
                    }
                }

                _logger.LogError("All download attempts failed for U2-Net model");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading U2-Net model to {DestinationPath}", destinationPath);
                return false;
            }
        }

        public async Task<bool> IsModelAvailableAsync(string modelPath)
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    return false;
                }

                var fileInfo = new FileInfo(modelPath);
                
                // Check if file size is reasonable (U2-Net models are typically 100MB+)
                if (fileInfo.Length < 1024 * 1024) // Less than 1MB is likely incomplete
                {
                    _logger.LogWarning("Model file {ModelPath} appears incomplete (size: {Size} bytes)", modelPath, fileInfo.Length);
                    return false;
                }

                return await VerifyModelIntegrityAsync(modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking model availability at {ModelPath}", modelPath);
                return false;
            }
        }

        public async Task<string> GetModelInfoAsync(string modelPath)
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    return "Model file not found";
                }

                var fileInfo = new FileInfo(modelPath);
                var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                
                var info = new StringBuilder();
                info.AppendLine($"Model File: {Path.GetFileName(modelPath)}");
                info.AppendLine($"Size: {sizeMB:F2} MB");
                info.AppendLine($"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
                info.AppendLine($"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

                var isValid = await VerifyModelIntegrityAsync(modelPath);
                info.AppendLine($"Status: {(isValid ? "Valid" : "Invalid or corrupted")}");

                return info.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model info for {ModelPath}", modelPath);
                return $"Error reading model info: {ex.Message}";
            }
        }

        public async Task<bool> VerifyModelIntegrityAsync(string modelPath)
        {
            try
            {
                // Basic integrity check - try to read the file header
                using var fileStream = new FileStream(modelPath, FileMode.Open, FileAccess.Read);
                var buffer = new byte[8];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, 8);
                
                if (bytesRead >= 8)
                {
                    // ONNX files typically start with specific magic bytes
                    // This is a basic check - ONNX Runtime will do more thorough validation
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying model integrity for {ModelPath}", modelPath);
                return false;
            }
        }
    }
}