using Members.Data;
using Members.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Members.Services
{
    public class ImageCompositionService(
        ApplicationDbContext context,
        ILogger<ImageCompositionService> logger,
        ISegmentationService segmentationService,
        IWebHostEnvironment environment) : IImageCompositionService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly ILogger<ImageCompositionService> _logger = logger;
        private readonly ISegmentationService _segmentationService = segmentationService;
        private readonly string _imagesPath = Path.Combine(environment.WebRootPath, "Images");

        public async Task<(bool Success, string Message, string? ProcessedImagePath)> ReplaceBackgroundAsync(
            string originalImagePath, string backgroundImagePath, string outputPath)
        {
            try
            {
                // Validate inputs
                if (!System.IO.File.Exists(originalImagePath) || !System.IO.File.Exists(backgroundImagePath))
                {
                    return (false, "Source images not found", null);
                }

                // Generate mask for the subject
                var maskBytes = await GenerateSubjectMaskAsync(originalImagePath);
                if (maskBytes == null)
                {
                    return (false, "Failed to generate subject mask", null);
                }

                // Perform background replacement
                using var originalImage = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(originalImagePath);
                using var backgroundImage = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(backgroundImagePath);
                using var maskImage = await SixLabors.ImageSharp.Image.LoadAsync<L8>(new MemoryStream(maskBytes));

                // Resize background to match original image
                backgroundImage.Mutate(x => x.Resize(originalImage.Width, originalImage.Height));

                // Create output image
                using var outputImage = new Image<Rgba32>(originalImage.Width, originalImage.Height);

                // Advanced blending with feathering and lighting preservation
                await BlendImagesWithAdvancedMasking(originalImage, backgroundImage, maskImage, outputImage);

                // Apply subtle edge enhancement
                outputImage.Mutate(x => x.GaussianBlur(0.3f));

                // Save the result
                await outputImage.SaveAsJpegAsync(outputPath);

                var relativePath = Path.GetRelativePath(_imagesPath, outputPath).Replace('\\', '/');
                return (true, "Background replaced successfully", $"/Images/{relativePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing background for image {OriginalPath}", originalImagePath);
                return (false, $"Error processing image: {ex.Message}", null);
            }
        }

        public async Task<byte[]?> GenerateSubjectMaskAsync(string imagePath)
        {
            return await _segmentationService.GenerateSegmentationMaskAsync(imagePath);
        }

        public async Task<(bool Success, string Message, string? ProcessedImagePath)> ComposeImageAsync(
            string subjectImagePath, string backgroundImagePath, string outputPath, CompositionOptions? options = null)
        {
            options ??= new CompositionOptions();

            try
            {
                using var subjectImage = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(subjectImagePath);
                using var backgroundImage = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(backgroundImagePath);

                // Resize background to match subject
                backgroundImage.Mutate(x => x.Resize(subjectImage.Width, subjectImage.Height));

                // Apply background blur if specified
                if (options.BackgroundBlur > 0)
                {
                    backgroundImage.Mutate(x => x.GaussianBlur(options.BackgroundBlur));
                }

                // Apply lighting adjustments
                if (options.LightingAdjustment != 0)
                {
                    backgroundImage.Mutate(x => x.Brightness(options.LightingAdjustment));
                }

                // Create composite image by copying background
                using var compositeImage = backgroundImage.Clone();
                
                // Simple overlay - copy subject pixels over background
                // This is a basic implementation - can be enhanced with proper alpha blending
                for (int y = 0; y < Math.Min(subjectImage.Height, compositeImage.Height); y++)
                {
                    for (int x = 0; x < Math.Min(subjectImage.Width, compositeImage.Width); x++)
                    {
                        var subjectPixel = subjectImage[x, y];
                        if (subjectPixel.A > 0) // If subject pixel is not transparent
                        {
                            compositeImage[x, y] = subjectPixel;
                        }
                    }
                }

                // Add watermark if specified
                if (!string.IsNullOrEmpty(options.WatermarkText))
                {
                    AddWatermark(compositeImage, options.WatermarkText);
                }

                // Save result
                await compositeImage.SaveAsJpegAsync(outputPath);

                var relativePath = Path.GetRelativePath(_imagesPath, outputPath).Replace('\\', '/');
                return (true, "Image composed successfully", $"/Images/{relativePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error composing image");
                return (false, $"Error composing image: {ex.Message}", null);
            }
        }

        public async Task<List<Background>> GetAvailableBackgroundsAsync(
            string? category = null, bool isPremiumUser = false, string? waterType = null)
        {
            var query = _context.Backgrounds.AsQueryable();

            // Filter by category
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(b => b.Category == category);
            }

            // Filter by water type
            if (!string.IsNullOrEmpty(waterType))
            {
                query = query.Where(b => b.WaterType == waterType || b.WaterType == "Both");
            }

            // Filter by premium status
            if (!isPremiumUser)
            {
                query = query.Where(b => !b.IsPremium);
            }

            return await query.OrderBy(b => b.Category).ThenBy(b => b.Name).ToListAsync();
        }

        public async Task<ImageValidationResult> ValidateImageForProcessingAsync(string imagePath)
        {
            try
            {
                if (!System.IO.File.Exists(imagePath))
                {
                    return new ImageValidationResult
                    {
                        IsValid = false,
                        Message = "Image file not found"
                    };
                }

                using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);
                var recommendations = new List<string>();
                var isValid = true;

                // Check image dimensions
                if (image.Width < 400 || image.Height < 400)
                {
                    recommendations.Add("Image resolution should be at least 400x400 pixels for better results");
                }

                // Check aspect ratio
                var aspectRatio = (float)image.Width / image.Height;
                if (aspectRatio < 0.5f || aspectRatio > 2.0f)
                {
                    recommendations.Add("Images with extreme aspect ratios may not process well");
                }

                // Check file size
                var fileInfo = new FileInfo(imagePath);
                if (fileInfo.Length > 10 * 1024 * 1024) // 10MB
                {
                    recommendations.Add("Large images may take longer to process");
                }

                // Simple subject detection (placeholder)
                var hasDetectedSubject = await DetectSubjectInImageAsync(imagePath);
                var confidence = hasDetectedSubject ? 0.8f : 0.3f;

                if (!hasDetectedSubject)
                {
                    recommendations.Add("No clear subject detected - background replacement may not work well");
                }

                return new ImageValidationResult
                {
                    IsValid = isValid,
                    Message = isValid ? "Image is suitable for processing" : "Image may have issues",
                    Recommendations = recommendations,
                    HasDetectedSubject = hasDetectedSubject,
                    ConfidenceScore = confidence
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating image {ImagePath}", imagePath);
                return new ImageValidationResult
                {
                    IsValid = false,
                    Message = $"Error validating image: {ex.Message}"
                };
            }
        }

        private async Task BlendImagesWithAdvancedMasking(
            Image<Rgba32> originalImage, 
            Image<Rgba32> backgroundImage, 
            Image<L8> maskImage, 
            Image<Rgba32> outputImage)
        {
            // Create feathered mask for smoother edges
            using var featheredMask = maskImage.Clone();
            featheredMask.Mutate(x => x.GaussianBlur(2.0f));

            await Task.Run(() =>
            {
                Parallel.For(0, originalImage.Height, y =>
                {
                    for (int x = 0; x < originalImage.Width; x++)
                    {
                        var maskPixel = featheredMask[x, y];
                        var alpha = maskPixel.PackedValue / 255f;

                        // Apply feathering curve for more natural edges
                        alpha = SmoothStep(alpha);

                        var originalPixel = originalImage[x, y];
                        var backgroundPixel = backgroundImage[x, y];

                        // Advanced blending with edge preservation
                        var blendedPixel = BlendPixelsAdvanced(originalPixel, backgroundPixel, alpha);
                        outputImage[x, y] = blendedPixel;
                    }
                });
            });
        }

        private float SmoothStep(float t)
        {
            // Smooth step function for natural edge transitions
            return t * t * (3.0f - 2.0f * t);
        }

        private Rgba32 BlendPixelsAdvanced(Rgba32 foreground, Rgba32 background, float alpha)
        {
            // Advanced alpha blending with gamma correction
            var invAlpha = 1.0f - alpha;
            
            // Apply gamma correction for more natural color blending
            var r = Math.Pow(Math.Pow(foreground.R / 255.0, 2.2) * alpha + Math.Pow(background.R / 255.0, 2.2) * invAlpha, 1.0 / 2.2);
            var g = Math.Pow(Math.Pow(foreground.G / 255.0, 2.2) * alpha + Math.Pow(background.G / 255.0, 2.2) * invAlpha, 1.0 / 2.2);
            var b = Math.Pow(Math.Pow(foreground.B / 255.0, 2.2) * alpha + Math.Pow(background.B / 255.0, 2.2) * invAlpha, 1.0 / 2.2);

            return new Rgba32(
                (byte)(Math.Clamp(r * 255.0, 0, 255)),
                (byte)(Math.Clamp(g * 255.0, 0, 255)),
                (byte)(Math.Clamp(b * 255.0, 0, 255)),
                255
            );
        }

        private Rgba32 GetDominantBackgroundColor(Image<Rgba32> image)
        {
            // Sample edge pixels to determine background color
            var edgePixels = new List<Rgba32>();
            
            // Sample top and bottom edges
            for (int x = 0; x < image.Width; x += 10)
            {
                edgePixels.Add(image[x, 0]);
                edgePixels.Add(image[x, image.Height - 1]);
            }
            
            // Sample left and right edges
            for (int y = 0; y < image.Height; y += 10)
            {
                edgePixels.Add(image[0, y]);
                edgePixels.Add(image[image.Width - 1, y]);
            }

            // Calculate average color
            long r = 0, g = 0, b = 0;
            foreach (var pixel in edgePixels)
            {
                r += pixel.R;
                g += pixel.G;
                b += pixel.B;
            }

            return new Rgba32(
                (byte)(r / edgePixels.Count),
                (byte)(g / edgePixels.Count),
                (byte)(b / edgePixels.Count),
                255
            );
        }

        private void AddWatermark(Image<Rgba32> image, string watermarkText)
        {
            // Simple text watermark implementation
            // TODO: Implement proper text rendering with ImageSharp.Drawing
            // For now, this is a placeholder
        }

        public async Task<byte[]?> AddWatermarkToImageAsync(string imagePath)
        {
            try
            {
                using var image = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(imagePath);
                
                // Add Fish-Smart watermark logo/text
                AddFishSmartWatermark(image);
                
                using var outputStream = new MemoryStream();
                await image.SaveAsJpegAsync(outputStream);
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding watermark to image {ImagePath}", imagePath);
                return null;
            }
        }

        private void AddFishSmartWatermark(Image<Rgba32> image)
        {
            try
            {
                // Create a semi-transparent overlay in bottom-right corner
                var watermarkWidth = Math.Max(200, image.Width / 4);
                var watermarkHeight = Math.Max(60, image.Height / 10);
                var x = image.Width - watermarkWidth - 20;
                var y = image.Height - watermarkHeight - 20;

                // Create simple watermark background using pixels
                var blackTransparent = new Rgba32(0, 0, 0, 180); // Semi-transparent black
                var whiteTransparent = new Rgba32(255, 255, 255, 220); // Semi-transparent white

                // Draw background rectangle
                for (int py = y; py < y + watermarkHeight && py < image.Height; py++)
                {
                    for (int px = x; px < x + watermarkWidth && px < image.Width; px++)
                    {
                        if (px >= 0 && py >= 0)
                        {
                            image[px, py] = blackTransparent;
                        }
                    }
                }

                // Draw border
                for (int i = 0; i < 2; i++)
                {
                    // Top and bottom borders
                    for (int px = x + i; px < x + watermarkWidth - i && px < image.Width; px++)
                    {
                        if (px >= 0 && y + i >= 0 && y + i < image.Height)
                            image[px, y + i] = whiteTransparent;
                        if (px >= 0 && y + watermarkHeight - 1 - i >= 0 && y + watermarkHeight - 1 - i < image.Height)
                            image[px, y + watermarkHeight - 1 - i] = whiteTransparent;
                    }
                    // Left and right borders
                    for (int py = y + i; py < y + watermarkHeight - i && py < image.Height; py++)
                    {
                        if (py >= 0 && x + i >= 0 && x + i < image.Width)
                            image[x + i, py] = whiteTransparent;
                        if (py >= 0 && x + watermarkWidth - 1 - i >= 0 && x + watermarkWidth - 1 - i < image.Width)
                            image[x + watermarkWidth - 1 - i, py] = whiteTransparent;
                    }
                }

                // Add text (simplified implementation without ImageSharp.Drawing)
                AddSimpleTextWatermark(image, "Fish-Smart", x + 10, y + 15);
                AddSimpleTextWatermark(image, "Get Premium for", x + 10, y + 30);
                AddSimpleTextWatermark(image, "watermark-free images", x + 10, y + 45);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding Fish-Smart watermark");
            }
        }

        private void AddSimpleTextWatermark(Image<Rgba32> image, string text, int startX, int startY)
        {
            // Very basic pixel-based text rendering (just for demo - in production use ImageSharp.Drawing)
            var white = new Rgba32(255, 255, 255, 255);
            var fontSize = 12;
            
            for (int i = 0; i < Math.Min(text.Length, 25); i++)
            {
                var charX = startX + (i * fontSize);
                if (charX + fontSize < image.Width && startY + fontSize < image.Height)
                {
                    // Draw a simple vertical line for each character (placeholder)
                    for (int py = startY; py < startY + fontSize && py < image.Height; py++)
                    {
                        if (charX >= 0 && charX < image.Width)
                        {
                            image[charX, py] = white;
                            if (charX + 1 < image.Width) image[charX + 1, py] = white;
                        }
                    }
                }
            }
        }

        private async Task<bool> DetectSubjectInImageAsync(string imagePath)
        {
            // Placeholder for subject detection
            // TODO: Implement with ONNX model
            await Task.Delay(100); // Simulate processing time
            return true; // For now, assume all images have subjects
        }
    }
}