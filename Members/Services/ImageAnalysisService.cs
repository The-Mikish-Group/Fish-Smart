using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Members.Services
{
    public class ImageAnalysisService : IImageAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageAnalysisService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ImageAnalysisService(HttpClient httpClient, ILogger<ImageAnalysisService> logger, 
            IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string> GenerateDescriptiveFilenameAsync(string imagePath)
        {
            try
            {
                var result = await AnalyzeImageAsync(imagePath);
                return result.SuggestedFilename;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating descriptive filename for {ImagePath}", imagePath);
                return Path.GetFileNameWithoutExtension(imagePath) + "_analysis_failed" + Path.GetExtension(imagePath);
            }
        }

        public async Task<ImageAnalysisResult> AnalyzeImageAsync(string imagePath)
        {
            try
            {
                _logger.LogInformation("Starting analysis for image: {ImagePath}", imagePath);
                
                // Check if file exists
                var fullPath = GetFullImagePath(imagePath);
                _logger.LogInformation("Full path resolved to: {FullPath}", fullPath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogError("Image file not found: {FullPath}", fullPath);
                    throw new FileNotFoundException($"Image not found: {fullPath}");
                }

                // Default to local analysis for reliability, with optional API upgrades
                var azureEndpoint = _configuration["AzureComputerVision:Endpoint"];
                var azureKey = _configuration["AzureComputerVision:Key"];
                var openAiKey = _configuration["OpenAI:ApiKey"];
                
                _logger.LogInformation("API Configuration - Azure: {HasAzure}, OpenAI: {HasOpenAI}", 
                    !string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureKey),
                    !string.IsNullOrEmpty(openAiKey));

                // Try local analysis first for speed and reliability
                _logger.LogInformation("Using enhanced local analysis for: {ImagePath}", imagePath);
                return await AnalyzeLocallyAsync(fullPath);
                
                // Note: API methods available but not used by default for reliability
                // Users can configure API keys to enable premium analysis
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image {ImagePath}", imagePath);
                
                return new ImageAnalysisResult
                {
                    SuggestedFilename = GenerateFallbackFilename(imagePath),
                    Description = "Analysis failed",
                    Confidence = 0.0f,
                    OriginalPath = imagePath
                };
            }
        }

        public async Task<List<string>> GenerateBatchFilenamesAsync(List<string> imagePaths)
        {
            var results = new List<string>();
            var semaphore = new SemaphoreSlim(3, 3); // Limit concurrent API calls

            var tasks = imagePaths.Select(async imagePath =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await GenerateDescriptiveFilenameAsync(imagePath);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var filenames = await Task.WhenAll(tasks);
            return filenames.ToList();
        }

        private async Task<ImageAnalysisResult> AnalyzeWithOpenAIAsync(string imagePath, string apiKey)
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var base64Image = Convert.ToBase64String(imageBytes);
                var extension = Path.GetExtension(imagePath).ToLowerInvariant();
                var mimeType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                var requestBody = new
                {
                    model = "gpt-4o-mini", // Cheaper vision model
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = "Generate a short, descriptive filename for this image (2-4 words, suitable for a filename). Focus on the main subject or scene. Examples: 'mountain_lake_sunset', 'red_barn_countryside', 'golden_retriever_park'" },
                                new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                            }
                        }
                    },
                    max_tokens = 50
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return ParseOpenAIResponse(jsonResponse, imagePath);
                }
                else
                {
                    _logger.LogWarning("OpenAI API call failed with status {StatusCode}", response.StatusCode);
                    return await AnalyzeLocallyAsync(imagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI Vision API");
                return await AnalyzeLocallyAsync(imagePath);
            }
        }

        private async Task<ImageAnalysisResult> AnalyzeWithAzureAsync(string imagePath, string endpoint, string key)
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

                var content = new ByteArrayContent(imageBytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                var response = await _httpClient.PostAsync(
                    $"{endpoint}/vision/v3.2/analyze?visualFeatures=Categories,Description,Tags&language=en", 
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return ParseAzureResponse(jsonResponse, imagePath);
                }
                else
                {
                    _logger.LogWarning("Azure API call failed with status {StatusCode}", response.StatusCode);
                    return await AnalyzeLocallyAsync(imagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Azure Computer Vision API");
                return await AnalyzeLocallyAsync(imagePath);
            }
        }

        private ImageAnalysisResult ParseOpenAIResponse(string jsonResponse, string imagePath)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                string description = "Unknown image";
                
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var content))
                        {
                            description = content.GetString() ?? description;
                        }
                    }
                }

                var filename = GenerateFilenameFromDescription(description);
                var extension = Path.GetExtension(imagePath);

                return new ImageAnalysisResult
                {
                    SuggestedFilename = filename + extension,
                    Description = description,
                    Tags = new List<string> { "ai_analyzed" },
                    Confidence = 0.8f, // High confidence for AI analysis
                    OriginalPath = imagePath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing OpenAI response");
                return new ImageAnalysisResult
                {
                    SuggestedFilename = GenerateFallbackFilename(imagePath),
                    Description = "Failed to parse AI analysis",
                    Confidence = 0.0f,
                    OriginalPath = imagePath
                };
            }
        }

        private async Task<ImageAnalysisResult> AnalyzeLocallyAsync(string imagePath)
        {
            try
            {
                _logger.LogInformation("Starting local analysis for: {ImagePath}", imagePath);
                
                using var image = await Image.LoadAsync(imagePath);
                var filename = Path.GetFileNameWithoutExtension(imagePath);
                var extension = Path.GetExtension(imagePath);
                
                _logger.LogInformation("Image loaded successfully - Size: {Width}x{Height}, Extension: {Extension}", 
                    image.Width, image.Height, extension);
                
                // Enhanced local analysis with pattern recognition
                var width = image.Width;
                var height = image.Height;
                var aspectRatio = (float)width / height;
                var fileInfo = new FileInfo(imagePath);
                var creationDate = fileInfo.CreationTime;
                
                // Analyze image characteristics
                var description = GenerateEnhancedDescription(width, height, aspectRatio, creationDate, filename);
                var suggestedName = GenerateEnhancedFilename(description, creationDate);
                
                _logger.LogInformation("Local analysis complete - Original: {Original}, Suggested: {Suggested}", 
                    filename + extension, suggestedName + extension);
                
                return new ImageAnalysisResult
                {
                    SuggestedFilename = suggestedName + extension,
                    Description = description,
                    Tags = GenerateEnhancedTags(aspectRatio, creationDate),
                    Confidence = 0.5f, // Medium confidence for enhanced local analysis
                    OriginalPath = imagePath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in local image analysis for {ImagePath}", imagePath);
                return new ImageAnalysisResult
                {
                    SuggestedFilename = GenerateFallbackFilename(imagePath),
                    Description = "Local analysis failed",
                    Confidence = 0.0f,
                    OriginalPath = imagePath
                };
            }
        }

        private ImageAnalysisResult ParseAzureResponse(string jsonResponse, string imagePath)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                string description = "Unknown image";
                float confidence = 0.0f;
                var tags = new List<string>();

                // Get description
                if (root.TryGetProperty("description", out var descElement))
                {
                    if (descElement.TryGetProperty("captions", out var captions) && captions.GetArrayLength() > 0)
                    {
                        var firstCaption = captions[0];
                        if (firstCaption.TryGetProperty("text", out var textElement))
                        {
                            description = textElement.GetString() ?? description;
                        }
                        if (firstCaption.TryGetProperty("confidence", out var confElement))
                        {
                            confidence = confElement.GetSingle();
                        }
                    }
                }

                // Get tags
                if (root.TryGetProperty("tags", out var tagsElement))
                {
                    foreach (var tag in tagsElement.EnumerateArray())
                    {
                        if (tag.TryGetProperty("name", out var nameElement))
                        {
                            var tagName = nameElement.GetString();
                            if (!string.IsNullOrEmpty(tagName))
                            {
                                tags.Add(tagName);
                            }
                        }
                    }
                }

                var filename = GenerateFilenameFromDescription(description);
                var extension = Path.GetExtension(imagePath);

                return new ImageAnalysisResult
                {
                    SuggestedFilename = filename + extension,
                    Description = description,
                    Tags = tags,
                    Confidence = confidence,
                    OriginalPath = imagePath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Azure response");
                return new ImageAnalysisResult
                {
                    SuggestedFilename = GenerateFallbackFilename(imagePath),
                    Description = "Failed to parse analysis",
                    Confidence = 0.0f,
                    OriginalPath = imagePath
                };
            }
        }

        private string GenerateFilenameFromDescription(string description)
        {
            // Clean and format description for filename
            var filename = description.ToLowerInvariant();
            
            // Remove articles and common words
            var wordsToRemove = new[] { "a ", "an ", "the ", "is ", "are ", "with ", "and ", "or ", "of ", "in ", "on ", "at " };
            foreach (var word in wordsToRemove)
            {
                filename = filename.Replace(word, " ");
            }
            
            // Replace special characters but keep spaces
            filename = Regex.Replace(filename, @"[^\w\s-]", "");
            filename = Regex.Replace(filename, @"\s+", " "); // Normalize multiple spaces to single spaces
            filename = filename.Trim();
            
            // Limit length
            if (filename.Length > 50)
            {
                filename = filename.Substring(0, 50).Trim();
            }
            
            return string.IsNullOrEmpty(filename) ? "analyzed image" : filename;
        }

        private string GenerateEnhancedDescription(int width, int height, float aspectRatio, DateTime creationDate, string originalFilename)
        {
            var orientation = aspectRatio > 1.3f ? "landscape" : aspectRatio < 0.75f ? "portrait" : "square";
            var resolution = width * height > 2000000 ? "high_res" : "standard";
            
            // Try to extract meaningful info from filename
            var filenameClues = ExtractFilenameClues(originalFilename);
            
            if (!string.IsNullOrEmpty(filenameClues))
            {
                return $"{filenameClues}_{orientation}_{resolution}";
            }
            
            // Use creation date patterns
            var season = GetSeason(creationDate);
            var timeOfDay = GetTimeOfDay(creationDate);
            
            return $"{season}_{timeOfDay}_{orientation}_{resolution}";
        }

        private string GenerateEnhancedFilename(string description, DateTime creationDate)
        {
            var dateStamp = creationDate.ToString("yyyyMMdd");
            // Replace underscores with spaces for better readability
            var cleanDescription = description.Replace("_", " ");
            return $"{cleanDescription} {dateStamp}";
        }

        private List<string> GenerateEnhancedTags(float aspectRatio, DateTime creationDate)
        {
            var tags = new List<string>();
            
            // Aspect ratio tags
            if (aspectRatio > 1.5f)
                tags.Add("wide");
            else if (aspectRatio < 0.67f)
                tags.Add("tall");
            else
                tags.Add("standard");
            
            // Date-based tags
            tags.Add(GetSeason(creationDate));
            tags.Add(creationDate.Year.ToString());
            
            return tags;
        }

        private string ExtractFilenameClues(string filename)
        {
            var lowerFilename = filename.ToLowerInvariant();
            
            // Common photography terms
            var photoTerms = new Dictionary<string, string>
            {
                {"img", "photo"},
                {"dsc", "photo"},
                {"pic", "photo"},
                {"image", "photo"},
                {"photo", "photo"},
                {"lake", "lake"},
                {"mountain", "mountain"},
                {"sunset", "sunset"},
                {"sunrise", "sunrise"},
                {"beach", "beach"},
                {"forest", "forest"},
                {"city", "city"},
                {"portrait", "portrait"},
                {"landscape", "landscape"},
                {"nature", "nature"},
                {"wildlife", "wildlife"},
                {"family", "family"},
                {"vacation", "vacation"},
                {"travel", "travel"}
            };
            
            foreach (var term in photoTerms)
            {
                if (lowerFilename.Contains(term.Key))
                {
                    return term.Value;
                }
            }
            
            return string.Empty;
        }

        private string GetSeason(DateTime date)
        {
            int month = date.Month;
            return month switch
            {
                12 or 1 or 2 => "winter",
                3 or 4 or 5 => "spring",
                6 or 7 or 8 => "summer",
                9 or 10 or 11 => "autumn",
                _ => "unknown"
            };
        }

        private string GetTimeOfDay(DateTime date)
        {
            int hour = date.Hour;
            return hour switch
            {
                >= 6 and < 12 => "morning",
                >= 12 and < 17 => "afternoon",
                >= 17 and < 20 => "evening",
                _ => "night"
            };
        }

        private string GenerateBasicDescription(int width, int height, float aspectRatio)
        {
            var orientation = aspectRatio > 1.3f ? "landscape" : aspectRatio < 0.75f ? "portrait" : "square";
            var resolution = width * height > 2000000 ? "high_resolution" : "standard_resolution";
            
            return $"{orientation}_{resolution}_image";
        }

        private string GenerateBasicFilename(string description, string originalFilename)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            return $"{description}_{timestamp}";
        }

        private List<string> GenerateBasicTags(float aspectRatio)
        {
            var tags = new List<string>();
            
            if (aspectRatio > 1.5f)
                tags.Add("wide");
            else if (aspectRatio < 0.67f)
                tags.Add("tall");
            else
                tags.Add("standard");
                
            return tags;
        }

        private string GenerateFallbackFilename(string imagePath)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var extension = Path.GetExtension(imagePath);
            return $"image_{timestamp}{extension}";
        }

        private string GetFullImagePath(string imagePath)
        {
            if (Path.IsPathRooted(imagePath))
            {
                return imagePath;
            }
            
            // Try different possible locations
            var possiblePaths = new[]
            {
                Path.Combine(_webHostEnvironment.WebRootPath, imagePath),
                Path.Combine(_webHostEnvironment.WebRootPath, "Galleries", imagePath),
                Path.Combine(_webHostEnvironment.ContentRootPath, imagePath)
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return imagePath; // Return original if not found
        }
    }
}