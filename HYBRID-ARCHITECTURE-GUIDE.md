# Fish-Smart Hybrid Architecture Implementation Guide
*Step-by-step guide to separate Main Site (MyAsp.net) from AI Processing Container (DigitalOcean)*

## 🎯 **End Goal**
- **Main Site**: All UI, workflows, file uploads (persistent on MyAsp.net)
- **Container Site**: AI processing only (ONNX models, background replacement)
- **Shared Database**: Coordination between both sites
- **API Communication**: Main site calls container for AI processing

---

## 📋 **Phase 1: Prepare Container as AI Service (Day 1 Morning)**

### **Step 1: Create AI API Controller in Container Project**

Create new file: `Members/Controllers/Api/AIProcessingController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Members.Services;
using Members.Data;

namespace Members.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIProcessingController : ControllerBase
    {
        private readonly IImageCompositionService _imageCompositionService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AIProcessingController> _logger;

        public AIProcessingController(
            IImageCompositionService imageCompositionService,
            ApplicationDbContext context,
            ILogger<AIProcessingController> logger)
        {
            _imageCompositionService = imageCompositionService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("replace-background")]
        public async Task<IActionResult> ReplaceBackground([FromBody] BackgroundReplacementRequest request)
        {
            try
            {
                _logger.LogInformation("Background replacement request: {Request}", request);

                // Download source image from main site
                using var httpClient = new HttpClient();
                var sourceImageBytes = await httpClient.GetByteArrayAsync(request.SourceImageUrl);
                
                // Get background from database
                var background = await _context.Backgrounds.FindAsync(request.BackgroundId);
                if (background == null)
                    return NotFound("Background not found");

                // Create temp files for processing
                var tempDir = Path.GetTempPath();
                var sourceFile = Path.Combine(tempDir, $"source_{Guid.NewGuid()}.jpg");
                var backgroundFile = Path.Combine(tempDir, $"background_{Guid.NewGuid()}.jpg");
                var outputFile = Path.Combine(tempDir, $"output_{Guid.NewGuid()}.jpg");

                try
                {
                    // Save source image temporarily
                    await File.WriteAllBytesAsync(sourceFile, sourceImageBytes);
                    
                    // Get background image path (adjust for container environment)
                    var backgroundPath = GetPhysicalPath(background.ImageUrl);
                    
                    // Process background replacement
                    var result = await _imageCompositionService.ReplaceBackgroundAsync(
                        sourceFile, backgroundPath, outputFile);

                    if (result.Success && File.Exists(outputFile))
                    {
                        // Read processed image
                        var processedBytes = await File.ReadAllBytesAsync(outputFile);
                        
                        // Return as base64 or upload back to main site
                        var base64Image = Convert.ToBase64String(processedBytes);
                        
                        return Ok(new
                        {
                            success = true,
                            processedImage = base64Image,
                            message = "Background replaced successfully"
                        });
                    }
                    
                    return BadRequest(new { success = false, message = result.Message });
                }
                finally
                {
                    // Cleanup temp files
                    CleanupTempFiles(sourceFile, backgroundFile, outputFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background replacement failed");
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Processing failed: {ex.Message}",
                    errorType = ex.GetType().Name
                });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                services = new {
                    imageComposition = _imageCompositionService != null,
                    database = _context != null
                }
            });
        }

        private string GetPhysicalPath(string imageUrl)
        {
            // Container-specific path resolution
            imageUrl = imageUrl.Trim();
            var relativePath = imageUrl.TrimStart('/');
            return Path.Combine("/app/wwwroot", relativePath);
        }

        private void CleanupTempFiles(params string[] files)
        {
            foreach (var file in files)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp file: {File}", file);
                }
            }
        }
    }

    public class BackgroundReplacementRequest
    {
        public string SourceImageUrl { get; set; } = string.Empty;
        public int BackgroundId { get; set; }
        public string CallbackUrl { get; set; } = string.Empty; // Optional: for async processing
    }
}
```

### **Step 2: Add CORS Configuration (Container)**

In `Program.cs` of container project:

```csharp
// Add CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMainSite", policy =>
    {
        policy.WithOrigins("https://your-main-site.myasp.net") // Replace with actual URL
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Use CORS
app.UseCors("AllowMainSite");
```

### **Step 3: Test Container API**

Deploy container and test the health endpoint:
```bash
curl https://your-container-url.ondigitalocean.app/api/aiprocessing/health
```

---

## 📋 **Phase 2: Modify Main Site to Call Container (Day 1 Afternoon)**

### **Step 4: Create AI Service Client in Main Site**

Create new file in main project: `Services/AIProcessingService.cs`

```csharp
using System.Text;
using System.Text.Json;

namespace Members.Services
{
    public interface IAIProcessingService
    {
        Task<AIProcessingResult> ReplaceBackgroundAsync(string imageUrl, int backgroundId);
        Task<bool> IsServiceAvailableAsync();
    }

    public class AIProcessingService : IAIProcessingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIProcessingService> _logger;

        public AIProcessingService(HttpClient httpClient, IConfiguration configuration, ILogger<AIProcessingService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AIProcessingResult> ReplaceBackgroundAsync(string imageUrl, int backgroundId)
        {
            try
            {
                var containerUrl = _configuration["AIProcessing:ContainerUrl"]; // Add to appsettings
                var requestUrl = $"{containerUrl}/api/aiprocessing/replace-background";

                var request = new
                {
                    SourceImageUrl = imageUrl,
                    BackgroundId = backgroundId,
                    CallbackUrl = "" // Optional
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(requestUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    
                    if (result.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        var base64Image = result.GetProperty("processedImage").GetString();
                        return new AIProcessingResult
                        {
                            Success = true,
                            ProcessedImageBase64 = base64Image,
                            Message = "Background replaced successfully"
                        };
                    }
                }

                return new AIProcessingResult
                {
                    Success = false,
                    Message = $"AI processing failed: {responseJson}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call AI processing service");
                return new AIProcessingResult
                {
                    Success = false,
                    Message = $"Service communication failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var containerUrl = _configuration["AIProcessing:ContainerUrl"];
                var response = await _httpClient.GetAsync($"{containerUrl}/api/aiprocessing/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    public class AIProcessingResult
    {
        public bool Success { get; set; }
        public string? ProcessedImageBase64 { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
```

### **Step 5: Update Main Site ImageViewer Controller**

Modify the `ReplaceBackground` method in main site's `ImageViewerController`:

```csharp
// Add to constructor
private readonly IAIProcessingService _aiProcessingService;

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ReplaceBackground([FromBody] ReplaceBackgroundRequest request)
{
    try
    {
        // Validate ownership (existing code)
        var hasAccess = await ValidateImageAccessAsync(request.ImageType, request.SourceId, userId);
        if (!hasAccess)
            return Json(new { success = false, message = "Access denied" });

        // Get the full URL to the image (for container to download)
        var imageUrl = await GetImageUrlAsync(request.ImageType, request.SourceId);
        if (string.IsNullOrEmpty(imageUrl))
            return Json(new { success = false, message = "Image not found" });

        // Call AI processing service
        var result = await _aiProcessingService.ReplaceBackgroundAsync(imageUrl, request.BackgroundId);
        
        if (result.Success && !string.IsNullOrEmpty(result.ProcessedImageBase64))
        {
            // Save the processed image back to the main site
            var processedBytes = Convert.FromBase64String(result.ProcessedImageBase64);
            var imagePath = await GetImagePathAsync(request.ImageType, request.SourceId);
            
            // Create backup first
            var backupPath = CreateBackupPath(imagePath);
            File.Copy(imagePath, backupPath, true);
            
            // Save processed image
            await File.WriteAllBytesAsync(imagePath, processedBytes);
            
            return Json(new { 
                success = true, 
                message = "Background replaced successfully",
                newImageUrl = GetImageUrlFromPath(imagePath)
            });
        }
        
        return Json(new { success = false, message = result.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Background replacement failed");
        return Json(new { success = false, message = $"Error: {ex.Message}" });
    }
}

private async Task<string?> GetImageUrlAsync(string imageType, int sourceId)
{
    // Get the database URL and convert to full URL for container to access
    string? relativeUrl = imageType switch
    {
        "AlbumCover" => await _context.CatchAlbums
            .Where(a => a.Id == sourceId)
            .Select(a => a.CoverImageUrl)
            .FirstOrDefaultAsync(),
        "CatchPhoto" => await _context.Catches
            .Where(c => c.Id == sourceId)
            .Select(c => c.PhotoUrl)
            .FirstOrDefaultAsync(),
        _ => null
    };

    if (string.IsNullOrEmpty(relativeUrl)) return null;
    
    // Convert to full URL so container can download it
    var baseUrl = $"{Request.Scheme}://{Request.Host}";
    return $"{baseUrl}{relativeUrl.Trim()}";
}
```

### **Step 6: Register Services in Main Site**

In main site's `Program.cs`:

```csharp
// Register HTTP client for AI service
builder.Services.AddHttpClient<IAIProcessingService, AIProcessingService>();
builder.Services.AddScoped<IAIProcessingService, AIProcessingService>();
```

### **Step 7: Add Configuration**

In main site's `appsettings.json`:

```json
{
  "AIProcessing": {
    "ContainerUrl": "https://your-container-url.ondigitalocean.app"
  }
}
```

---

## 📋 **Phase 3: Testing & Validation (Day 2)**

### **Step 8: Test the Complete Flow**

1. **Deploy container** with AI API
2. **Update main site** with AI service client
3. **Test background replacement**:
   - Upload image to main site (persistent)
   - Try background replacement
   - Should call container, process, return result
   - Image stays on main site

### **Step 9: Monitor and Debug**

Check logs in both places:
- **Main site**: Service communication logs
- **Container**: AI processing logs
- **Browser console**: Client-side errors

---

## 🔧 **Configuration Checklist**

### **Container Site (.env or appsettings)**
- ✅ Database connection string
- ✅ CORS configuration for main site
- ✅ File paths for background images

### **Main Site (appsettings.json)**
- ✅ AI Processing container URL
- ✅ HTTP client timeout settings
- ✅ Fallback options if container unavailable

### **Database**
- ✅ Accessible from both sites
- ✅ Connection strings configured
- ✅ Background images table populated

---

## 🚀 **Future Enhancements**

### **Phase 4: Additional AI Services**
- Fish species recognition
- Voice processing
- Weather integration
- Advanced image analysis

### **Phase 5: Performance Optimization**
- Async processing with callbacks
- Queue system for heavy operations
- Caching strategies
- CDN integration

---

## 🛟 **Troubleshooting Guide**

### **Common Issues**
1. **CORS errors**: Check container CORS configuration
2. **Timeout errors**: Increase HTTP client timeout
3. **File not found**: Verify image URLs are accessible from container
4. **Database connection**: Ensure both sites can access shared database

### **Fallback Strategy**
If container is unavailable:
- Show user-friendly message
- Disable AI features temporarily
- Log issues for monitoring
- Consider local fallback processing

---

**This guide gives you a complete roadmap for tomorrow morning. Start with Phase 1, test each step, then move to Phase 2. Take it step by step and you'll have a working hybrid architecture!** 🎯