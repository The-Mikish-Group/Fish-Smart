using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics.Tensors;

namespace Members.Services
{
    public class SegmentationService : ISegmentationService, IDisposable
    {
        private readonly ILogger<SegmentationService> _logger;
        private readonly IWebHostEnvironment _environment;
        private InferenceSession? _session;
        private readonly string _modelsPath;
        private bool _disposed = false;

        public SegmentationService(ILogger<SegmentationService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
            _modelsPath = Path.Combine(_environment.WebRootPath, "Models");
        }

        public async Task<byte[]?> GenerateSegmentationMaskAsync(string imagePath)
        {
            try
            {
                // Ensure AI models are initialized if possible
                await EnsureAIModelsInitializedAsync();
                
                // Check if AI models are available, fallback to color-based if not
                if (IsAISegmentationAvailable())
                {
                    _logger.LogInformation("Using AI segmentation for {ImagePath}", imagePath);
                    return await GenerateAISegmentationMaskAsync(imagePath);
                }
                else
                {
                    _logger.LogInformation("AI segmentation not available, using enhanced color-based method for {ImagePath}", imagePath);
                    return await GenerateColorBasedMaskAsync(imagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating segmentation mask, falling back to color-based method");
                return await GenerateColorBasedMaskAsync(imagePath);
            }
        }

        private async Task EnsureAIModelsInitializedAsync()
        {
            // If session is null but model file exists, try to initialize
            if (_session == null && File.Exists(GetModelPath()))
            {
                _logger.LogInformation("AI model file found but not loaded. Attempting to initialize...");
                await InitializeAIModelsAsync();
            }
        }

        public bool IsAISegmentationAvailable()
        {
            var modelExists = File.Exists(GetModelPath());
            var sessionReady = _session != null;
            
            _logger.LogDebug("AI Segmentation status - Model exists: {ModelExists}, Session ready: {SessionReady}", modelExists, sessionReady);
            
            return sessionReady && modelExists;
        }

        public Task<bool> InitializeAIModelsAsync()
        {
            try
            {
                var modelPath = GetModelPath();
                
                if (!File.Exists(modelPath))
                {
                    _logger.LogWarning("AI segmentation model not found at {ModelPath}. Download required.", modelPath);
                    return Task.FromResult(false);
                }

                // Check file size - U2-Net models should be much larger than 4MB
                var fileInfo = new FileInfo(modelPath);
                _logger.LogInformation("Model file size: {FileSize} MB", fileInfo.Length / (1024.0 * 1024.0));
                
                if (fileInfo.Length < 50 * 1024 * 1024) // Less than 50MB is suspicious
                {
                    _logger.LogWarning("Model file seems too small ({Size} MB). Expected ~170MB for U2-Net. File may be incomplete.", fileInfo.Length / (1024.0 * 1024.0));
                    // Continue anyway to try loading
                }

                // Dispose existing session if any
                _session?.Dispose();

                // Create ONNX session with error handling and hosting-friendly options
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC, // More conservative for hosting
                    LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING
                };

                // Add execution providers in order of preference (CPU fallback for hosting compatibility)
                sessionOptions.AppendExecutionProvider_CPU(0);

                _logger.LogInformation("Attempting to load ONNX model from {ModelPath}", modelPath);
                _logger.LogInformation("Environment: {Environment}, OS: {OS}", _environment.EnvironmentName, Environment.OSVersion);
                
                _session = new InferenceSession(modelPath, sessionOptions);
                
                // Verify the model loaded by checking input/output metadata
                var inputMetadata = _session.InputMetadata;
                var outputMetadata = _session.OutputMetadata;
                
                _logger.LogInformation("Model loaded successfully. Inputs: {InputCount}, Outputs: {OutputCount}", 
                    inputMetadata.Count, outputMetadata.Count);
                
                foreach (var input in inputMetadata)
                {
                    _logger.LogInformation("Input: {Name}, Shape: {Shape}", input.Key, string.Join(",", input.Value.Dimensions));
                }
                
                return Task.FromResult(true);
            }
            catch (PlatformNotSupportedException ex)
            {
                _logger.LogError(ex, "ONNX Runtime not supported on this platform. AI segmentation disabled.");
                _session?.Dispose();
                _session = null;
                return Task.FromResult(false);
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError(ex, "ONNX Runtime native libraries not found. This is common in shared hosting environments. AI segmentation disabled.");
                _session?.Dispose();
                _session = null;
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AI segmentation models: {Message}. Check hosting environment ONNX Runtime support.", ex.Message);
                _session?.Dispose();
                _session = null;
                return Task.FromResult(false);
            }
        }

        private async Task<byte[]?> GenerateAISegmentationMaskAsync(string imagePath)
        {
            if (_session == null)
            {
                throw new InvalidOperationException("AI models not initialized");
            }

            try
            {
                _logger.LogInformation("Starting AI segmentation for image: {ImagePath}", imagePath);
                
                using var image = await Image.LoadAsync<Rgb24>(imagePath);
                var originalWidth = image.Width;
                var originalHeight = image.Height;
                
                _logger.LogInformation("Original image size: {Width}x{Height}", originalWidth, originalHeight);
                
                // U2-Net typically expects 320x320 input
                const int modelSize = 320;
                using var resizedImage = image.Clone(x => x.Resize(modelSize, modelSize));

                // Convert to tensor with proper normalization for U2-Net
                var tensor = new DenseTensor<float>(new[] { 1, 3, modelSize, modelSize });
                
                // U2-Net preprocessing: normalize to [-1, 1] range (ImageNet normalization)
                var meanR = 0.485f;
                var meanG = 0.456f; 
                var meanB = 0.406f;
                var stdR = 0.229f;
                var stdG = 0.224f;
                var stdB = 0.225f;
                
                for (int y = 0; y < modelSize; y++)
                {
                    for (int x = 0; x < modelSize; x++)
                    {
                        var pixel = resizedImage[x, y];
                        
                        // Normalize RGB values to [0,1] then apply ImageNet normalization
                        var r = (pixel.R / 255.0f - meanR) / stdR;
                        var g = (pixel.G / 255.0f - meanG) / stdG;
                        var b = (pixel.B / 255.0f - meanB) / stdB;
                        
                        tensor[0, 0, y, x] = r; // Red channel
                        tensor[0, 1, y, x] = g; // Green channel  
                        tensor[0, 2, y, x] = b; // Blue channel
                    }
                }

                // Run inference - use the actual input name from the model
                var inputName = _session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, tensor)
                };

                _logger.LogInformation("Running ONNX inference with input name: {InputName}", inputName);
                using var results = _session.Run(inputs);
                var output = results.First();
                var outputTensor = output.AsTensor<float>();
                
                _logger.LogInformation("Inference complete. Output shape: {Shape}", string.Join(",", outputTensor.Dimensions.ToArray()));

                // Convert output tensor to mask image
                using var maskImage = new Image<L8>(modelSize, modelSize);
                
                // Handle different output formats
                var outputDims = outputTensor.Dimensions.ToArray();
                if (outputDims.Length == 4) // [batch, channels, height, width]
                {
                    for (int y = 0; y < modelSize; y++)
                    {
                        for (int x = 0; x < modelSize; x++)
                        {
                            // Apply sigmoid to get probability
                            var rawValue = outputTensor[0, 0, y, x];
                            var sigmoid = 1.0f / (1.0f + (float)Math.Exp(-rawValue));
                            var value = Math.Max(0, Math.Min(1, sigmoid));
                            maskImage[x, y] = new L8((byte)(value * 255));
                        }
                    }
                }

                // Resize mask back to original image size
                using var finalMask = maskImage.Clone(x => x.Resize(originalWidth, originalHeight));
                
                // Enhanced post-processing for better results
                finalMask.Mutate(x => x
                    .GaussianBlur(0.5f)                     // Light smoothing
                    .BinaryThreshold(0.5f));                // Clean threshold
                
                _logger.LogInformation("AI segmentation completed successfully");

                // Convert to byte array
                using var stream = new MemoryStream();
                await finalMask.SaveAsPngAsync(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI segmentation: {Message}", ex.Message);
                throw;
            }
        }

        private async Task<byte[]?> GenerateColorBasedMaskAsync(string imagePath)
        {
            try
            {
                _logger.LogInformation("Using enhanced color-based segmentation for {ImagePath}", imagePath);
                
                using var image = await Image.LoadAsync<Rgba32>(imagePath);
                using var maskImage = new Image<L8>(image.Width, image.Height);

                // Enhanced multi-pass segmentation algorithm
                await PerformAdvancedColorSegmentation(image, maskImage);

                using var stream = new MemoryStream();
                await maskImage.SaveAsPngAsync(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in color-based segmentation");
                return null;
            }
        }

        private async Task PerformAdvancedColorSegmentation(Image<Rgba32> image, Image<L8> maskImage)
        {
            await Task.Run(() =>
            {
                // Step 1: Detect if this is a high-contrast scenario (like your fishing photo)
                var isHighContrast = DetectHighContrastScenario(image);
                
                if (isHighContrast)
                {
                    _logger.LogInformation("High contrast detected - using advanced edge-based segmentation");
                    PerformHighContrastSegmentation(image, maskImage);
                }
                else
                {
                    _logger.LogInformation("Standard contrast - using multi-color segmentation");
                    PerformStandardColorSegmentation(image, maskImage);
                }
            });
        }

        private bool DetectHighContrastScenario(Image<Rgba32> image)
        {
            var edgePixels = new List<double>();
            var sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 50);
            
            // Sample edge brightness
            for (int x = 0; x < image.Width; x += sampleStep)
            {
                edgePixels.Add(GetBrightness(image[x, 0]));
                edgePixels.Add(GetBrightness(image[x, image.Height - 1]));
            }
            
            for (int y = 0; y < image.Height; y += sampleStep)
            {
                edgePixels.Add(GetBrightness(image[0, y]));
                edgePixels.Add(GetBrightness(image[image.Width - 1, y]));
            }
            
            // Sample center brightness
            var centerX = image.Width / 2;
            var centerY = image.Height / 2;
            var centerRegionSize = Math.Min(image.Width, image.Height) / 6;
            var centerPixels = new List<double>();
            
            for (int x = centerX - centerRegionSize; x < centerX + centerRegionSize; x += 2)
            {
                for (int y = centerY - centerRegionSize; y < centerY + centerRegionSize; y += 2)
                {
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        centerPixels.Add(GetBrightness(image[x, y]));
                    }
                }
            }
            
            var avgEdgeBrightness = edgePixels.Average();
            var avgCenterBrightness = centerPixels.Average();
            var brightnessDifference = Math.Abs(avgCenterBrightness - avgEdgeBrightness);
            
            // High contrast if there's a significant brightness difference (like dark background, lit subject)
            return brightnessDifference > 0.3; // 30% brightness difference threshold
        }

        private double GetBrightness(Rgba32 pixel)
        {
            return (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B) / 255.0;
        }

        private void PerformHighContrastSegmentation(Image<Rgba32> image, Image<L8> maskImage)
        {
            // This handles scenarios like your night fishing photo
            using var edgeMap = new Image<L8>(image.Width, image.Height);
            
            // Step 1: Create edge detection map
            CreateAdvancedEdgeMap(image, edgeMap);
            
            // Step 2: Use brightness-based segmentation for high contrast
            var backgroundColors = GetMultipleBackgroundColors(image);
            var avgBackgroundBrightness = backgroundColors.Average(GetBrightness);
            
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    var brightness = GetBrightness(pixel);
                    var edgeStrength = edgeMap[x, y].PackedValue / 255.0;
                    
                    // Calculate center bias
                    var centerX = image.Width / 2.0f;
                    var centerY = image.Height / 2.0f;
                    var distanceFromCenter = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                    var maxDistance = Math.Sqrt(Math.Pow(centerX, 2) + Math.Pow(centerY, 2));
                    var centerBias = (1.0f - (distanceFromCenter / maxDistance)) * 0.3f;
                    
                    // Brightness-based probability
                    var brightnessDiff = Math.Abs(brightness - avgBackgroundBrightness);
                    var brightnessScore = Math.Min(1.0, brightnessDiff * 2.0); // Scale brightness difference
                    
                    // Edge-enhanced probability
                    var edgeEnhanced = brightnessScore + (edgeStrength * 0.4); // Boost edge pixels
                    
                    // Color distance check
                    var minColorDistance = backgroundColors.Min(bg => CalculateColorDistance(pixel, bg)) / 100.0;
                    var colorScore = Math.Min(1.0, minColorDistance);
                    
                    // Combined score with center bias
                    var finalScore = Math.Min(1.0, (edgeEnhanced * 0.6 + colorScore * 0.4 + centerBias));
                    
                    maskImage[x, y] = new L8((byte)(finalScore * 255));
                }
            }
            
            // Advanced morphological operations for high contrast
            maskImage.Mutate(x => x
                .GaussianBlur(1.0f)                    // Light smoothing
                .BinaryThreshold(0.45f)                // Slightly higher threshold
                .GaussianBlur(0.3f));                  // Minimal smoothing to preserve edges
        }

        private void CreateAdvancedEdgeMap(Image<Rgba32> image, Image<L8> edgeMap)
        {
            // Sobel edge detection
            for (int y = 1; y < image.Height - 1; y++)
            {
                for (int x = 1; x < image.Width - 1; x++)
                {
                    // Get surrounding pixels
                    var tl = GetBrightness(image[x - 1, y - 1]);
                    var tm = GetBrightness(image[x, y - 1]);
                    var tr = GetBrightness(image[x + 1, y - 1]);
                    var ml = GetBrightness(image[x - 1, y]);
                    var mr = GetBrightness(image[x + 1, y]);
                    var bl = GetBrightness(image[x - 1, y + 1]);
                    var bm = GetBrightness(image[x, y + 1]);
                    var br = GetBrightness(image[x + 1, y + 1]);
                    
                    // Sobel operators
                    var gx = (tr + 2 * mr + br) - (tl + 2 * ml + bl);
                    var gy = (bl + 2 * bm + br) - (tl + 2 * tm + tr);
                    
                    var magnitude = Math.Sqrt(gx * gx + gy * gy);
                    edgeMap[x, y] = new L8((byte)(Math.Min(255, magnitude * 255)));
                }
            }
        }

        private void PerformStandardColorSegmentation(Image<Rgba32> image, Image<L8> maskImage)
        {
            // Original enhanced algorithm for standard scenarios
            var backgroundColors = GetMultipleBackgroundColors(image);
            var centerWeight = 2.0f;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    var minBackgroundDistance = backgroundColors.Min(bg => CalculateColorDistance(pixel, bg));
                    
                    var centerX = image.Width / 2.0f;
                    var centerY = image.Height / 2.0f;
                    var distanceFromCenter = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                    var maxDistance = Math.Sqrt(Math.Pow(centerX, 2) + Math.Pow(centerY, 2));
                    var centerBias = (1.0f - (distanceFromCenter / maxDistance)) * centerWeight;
                    
                    var adjustedThreshold = 45 - (centerBias * 15);
                    var subjectProbability = Math.Max(0, Math.Min(255, 
                        (minBackgroundDistance - adjustedThreshold) * 4 + centerBias * 30));
                    
                    maskImage[x, y] = new L8((byte)subjectProbability);
                }
            }

            maskImage.Mutate(x => x
                .GaussianBlur(1.5f)                    // Smooth transitions
                .BinaryThreshold(0.4f)                 // Clean separation
                .GaussianBlur(0.5f));                  // Final smoothing
        }

        private List<Rgba32> GetMultipleBackgroundColors(Image<Rgba32> image)
        {
            var edgePixels = new List<Rgba32>();
            var sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 30);
            
            // Sample all edges more densely
            for (int x = 0; x < image.Width; x += sampleStep)
            {
                edgePixels.Add(image[x, 0]);
                edgePixels.Add(image[x, image.Height - 1]);
            }
            
            for (int y = 0; y < image.Height; y += sampleStep)
            {
                edgePixels.Add(image[0, y]);
                edgePixels.Add(image[image.Width - 1, y]);
            }
            
            // Add corner emphasis (corners are very likely background)
            var cornerSize = Math.Min(image.Width, image.Height) / 10;
            for (int x = 0; x < cornerSize; x += 2)
            {
                for (int y = 0; y < cornerSize; y += 2)
                {
                    edgePixels.Add(image[x, y]); // Top-left
                    edgePixels.Add(image[image.Width - 1 - x, y]); // Top-right
                    edgePixels.Add(image[x, image.Height - 1 - y]); // Bottom-left
                    edgePixels.Add(image[image.Width - 1 - x, image.Height - 1 - y]); // Bottom-right
                }
            }

            // Use k-means-like clustering to find dominant background colors
            return FindDominantColors(edgePixels, 3); // Find up to 3 dominant background colors
        }

        private List<Rgba32> FindDominantColors(List<Rgba32> pixels, int numColors)
        {
            if (pixels.Count == 0) return new List<Rgba32>();
            
            // Simple clustering - group similar colors
            var clusters = new List<List<Rgba32>>();
            var tolerance = 30;
            
            foreach (var pixel in pixels)
            {
                var foundCluster = false;
                foreach (var cluster in clusters)
                {
                    if (cluster.Count > 0 && CalculateColorDistance(pixel, cluster[0]) < tolerance)
                    {
                        cluster.Add(pixel);
                        foundCluster = true;
                        break;
                    }
                }
                
                if (!foundCluster)
                {
                    clusters.Add(new List<Rgba32> { pixel });
                }
            }
            
            // Return the centroids of the largest clusters
            return clusters
                .OrderByDescending(c => c.Count)
                .Take(numColors)
                .Select(cluster =>
                {
                    var avgR = (byte)cluster.Average(p => p.R);
                    var avgG = (byte)cluster.Average(p => p.G);
                    var avgB = (byte)cluster.Average(p => p.B);
                    return new Rgba32(avgR, avgG, avgB, 255);
                })
                .ToList();
        }

        private Rgba32 GetDominantBackgroundColor(Image<Rgba32> image)
        {
            var edgePixels = new List<Rgba32>();
            
            // Sample edges more densely
            var sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 50);
            
            // Sample all four edges
            for (int x = 0; x < image.Width; x += sampleStep)
            {
                edgePixels.Add(image[x, 0]);                           // Top edge
                edgePixels.Add(image[x, image.Height - 1]);            // Bottom edge
            }
            
            for (int y = 0; y < image.Height; y += sampleStep)
            {
                edgePixels.Add(image[0, y]);                           // Left edge
                edgePixels.Add(image[image.Width - 1, y]);             // Right edge
            }

            // Calculate median color (more robust than mean)
            var rValues = edgePixels.Select(p => (int)p.R).OrderBy(x => x).ToList();
            var gValues = edgePixels.Select(p => (int)p.G).OrderBy(x => x).ToList();
            var bValues = edgePixels.Select(p => (int)p.B).OrderBy(x => x).ToList();

            var medianR = rValues[rValues.Count / 2];
            var medianG = gValues[gValues.Count / 2];
            var medianB = bValues[bValues.Count / 2];

            return new Rgba32((byte)medianR, (byte)medianG, (byte)medianB, 255);
        }

        private double CalculateColorDistance(Rgba32 color1, Rgba32 color2)
        {
            // Use perceptual color distance (weighted for human vision)
            var rDiff = color1.R - color2.R;
            var gDiff = color1.G - color2.G;
            var bDiff = color1.B - color2.B;
            
            // Weights based on human eye sensitivity
            return Math.Sqrt(0.299 * rDiff * rDiff + 0.587 * gDiff * gDiff + 0.114 * bDiff * bDiff);
        }

        private string GetModelPath()
        {
            // For now, we'll use a placeholder path
            // In production, you'd download models like U2-Net or use a smaller ONNX model
            return Path.Combine(_modelsPath, "u2net.onnx");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
        }
    }
}