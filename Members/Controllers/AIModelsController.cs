using Members.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Members.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AIModelsController(
        ISegmentationService segmentationService, 
        IModelDownloadService modelDownloadService,
        IWebHostEnvironment environment,
        ILogger<AIModelsController> logger) : Controller
    {
        private readonly ISegmentationService _segmentationService = segmentationService;
        private readonly IModelDownloadService _modelDownloadService = modelDownloadService;
        private readonly IWebHostEnvironment _environment = environment;
        private readonly ILogger<AIModelsController> _logger = logger;

        // GET: AIModels
        public async Task<IActionResult> Index()
        {
            var modelPath = Path.Combine(_environment.WebRootPath, "Models", "u2net.onnx");
            var isModelAvailable = await _modelDownloadService.IsModelAvailableAsync(modelPath);
            var modelInfo = await _modelDownloadService.GetModelInfoAsync(modelPath);

            var viewModel = new AIModelsViewModel
            {
                IsAISegmentationAvailable = _segmentationService.IsAISegmentationAvailable(),
                ModelStatus = GetModelStatus(),
                ModelInfo = modelInfo,
                IsModelDownloaded = isModelAvailable,
                Instructions = GetInstallationInstructions(),
                DiagnosticInfo = GetDiagnosticInfo(modelPath)
            };

            return View(viewModel);
        }

        // POST: AIModels/InitializeModels
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitializeModels()
        {
            try
            {
                var success = await _segmentationService.InitializeAIModelsAsync();
                
                if (success)
                {
                    TempData["Success"] = "AI segmentation models initialized successfully.";
                }
                else
                {
                    TempData["Warning"] = "AI models not found. Please download the U2-Net model and place it in wwwroot/Models/u2net.onnx";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing AI models");
                TempData["Error"] = $"Error initializing AI models: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: AIModels/DownloadModel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadModel()
        {
            try
            {
                var modelPath = Path.Combine(_environment.WebRootPath, "Models", "u2net.onnx");
                
                _logger.LogInformation("Starting model download to {ModelPath}", modelPath);
                
                // Ensure the Models directory exists
                var modelsDir = Path.GetDirectoryName(modelPath);
                if (!Directory.Exists(modelsDir))
                {
                    Directory.CreateDirectory(modelsDir!);
                    _logger.LogInformation("Created Models directory at {ModelsDir}", modelsDir);
                }
                
                var downloadSuccess = await _modelDownloadService.DownloadU2NetModelAsync(modelPath);
                
                if (downloadSuccess)
                {
                    _logger.LogInformation("Model download successful, checking file exists: {FileExists}", System.IO.File.Exists(modelPath));
                    
                    // Try to initialize the model after download
                    var initializeSuccess = await _segmentationService.InitializeAIModelsAsync();
                    
                    if (initializeSuccess)
                    {
                        TempData["Success"] = "U2-Net model downloaded and initialized successfully! Background removal quality should now be significantly improved.";
                    }
                    else
                    {
                        TempData["Warning"] = "Model downloaded but failed to initialize. File exists: " + System.IO.File.Exists(modelPath) + ". Please try initializing manually or download manually using the link below.";
                    }
                }
                else
                {
                    TempData["Error"] = "Failed to download U2-Net model. Please use the manual download option below or check your internet connection.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading AI model");
                TempData["Error"] = $"Error downloading model: {ex.Message}. Please use the manual download option below.";
            }

            return RedirectToAction(nameof(Index));
        }

        private string GetModelStatus()
        {
            if (_segmentationService.IsAISegmentationAvailable())
            {
                return "AI segmentation models are loaded and ready.";
            }
            else
            {
                return "AI segmentation models are not available. Using color-based fallback method.";
            }
        }

        private List<string> GetInstallationInstructions()
        {
            return new List<string>
            {
                "Click 'Download U2-Net Model' to automatically download the AI model (recommended)",
                "Or manually download the U2-Net ONNX model and save as 'u2net.onnx' in wwwroot/Models",
                "After download, click 'Initialize AI Models' to load the model",
                "The AI model will significantly improve background removal quality",
                "Alternative: Configure cloud-based APIs (Azure Computer Vision, Google Vision) for best results"
            };
        }

        private string GetDiagnosticInfo(string modelPath)
        {
            var info = new System.Text.StringBuilder();
            
            info.AppendLine($"Environment: {_environment.EnvironmentName}");
            info.AppendLine($"OS: {Environment.OSVersion}");
            info.AppendLine($"Framework: {Environment.Version}");
            info.AppendLine($"Model Path: {modelPath}");
            info.AppendLine($"File Exists: {System.IO.File.Exists(modelPath)}");
            
            if (System.IO.File.Exists(modelPath))
            {
                var fileInfo = new System.IO.FileInfo(modelPath);
                info.AppendLine($"File Size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
                info.AppendLine($"Last Modified: {fileInfo.LastWriteTime}");
            }
            
            try
            {
                // Test ONNX Runtime availability
                var testOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
                info.AppendLine("ONNX Runtime: Available");
                testOptions.Dispose();
            }
            catch (Exception ex)
            {
                info.AppendLine($"ONNX Runtime Error: {ex.GetType().Name} - {ex.Message}");
            }
            
            return info.ToString();
        }
    }

    public class AIModelsViewModel
    {
        public bool IsAISegmentationAvailable { get; set; }
        public string ModelStatus { get; set; } = string.Empty;
        public string ModelInfo { get; set; } = string.Empty;
        public bool IsModelDownloaded { get; set; }
        public List<string> Instructions { get; set; } = new();
        public string DiagnosticInfo { get; set; } = string.Empty;
    }
}