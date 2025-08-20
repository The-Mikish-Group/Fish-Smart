using Members.Models;

namespace Members.Services
{
    public interface IImageCompositionService
    {
        /// <summary>
        /// Replaces the background of an image with a new background
        /// </summary>
        /// <param name="originalImagePath">Path to the original image</param>
        /// <param name="backgroundImagePath">Path to the new background image</param>
        /// <param name="outputPath">Path where the result should be saved</param>
        /// <returns>Success status and any error messages</returns>
        Task<(bool Success, string Message, string? ProcessedImagePath)> ReplaceBackgroundAsync(
            string originalImagePath, 
            string backgroundImagePath, 
            string outputPath);

        /// <summary>
        /// Generates a mask for the subject (person/fish) in the image
        /// </summary>
        /// <param name="imagePath">Path to the image to segment</param>
        /// <returns>Byte array of the mask image (white = subject, black = background)</returns>
        Task<byte[]?> GenerateSubjectMaskAsync(string imagePath);

        /// <summary>
        /// Composes a new image by combining subject, background, and optional effects
        /// </summary>
        /// <param name="subjectImagePath">Path to subject image</param>
        /// <param name="backgroundImagePath">Path to background image</param>
        /// <param name="outputPath">Output path for composed image</param>
        /// <param name="options">Composition options (blur, lighting, etc.)</param>
        /// <returns>Success status and result path</returns>
        Task<(bool Success, string Message, string? ProcessedImagePath)> ComposeImageAsync(
            string subjectImagePath,
            string backgroundImagePath, 
            string outputPath,
            CompositionOptions? options = null);

        /// <summary>
        /// Gets available backgrounds filtered by category and subscription level
        /// </summary>
        /// <param name="category">Background category (optional)</param>
        /// <param name="isPremiumUser">Whether user has premium access</param>
        /// <param name="waterType">Water type filter (optional)</param>
        /// <returns>List of available backgrounds</returns>
        Task<List<Background>> GetAvailableBackgroundsAsync(
            string? category = null, 
            bool isPremiumUser = false,
            string? waterType = null);

        /// <summary>
        /// Validates if an image is suitable for background replacement
        /// </summary>
        /// <param name="imagePath">Path to image to validate</param>
        /// <returns>Validation result with recommendations</returns>
        Task<ImageValidationResult> ValidateImageForProcessingAsync(string imagePath);

        /// <summary>
        /// Adds a watermark to an image and returns the watermarked image as bytes
        /// </summary>
        /// <param name="imagePath">Path to the original image</param>
        /// <returns>Watermarked image as byte array</returns>
        Task<byte[]?> AddWatermarkToImageAsync(string imagePath);

        /// <summary>
        /// Composites a transparent subject image (from Remove.bg) with a background
        /// </summary>
        /// <param name="transparentSubjectPath">Path to transparent subject image</param>
        /// <param name="backgroundImagePath">Path to background image</param>
        /// <param name="outputPath">Path where result should be saved</param>
        /// <returns>Success status and message</returns>
        Task<(bool Success, string Message)> CompositeTransparentImageWithBackgroundAsync(
            string transparentSubjectPath,
            string backgroundImagePath, 
            string outputPath);
    }

    public class CompositionOptions
    {
        public float BackgroundBlur { get; set; } = 0f; // 0-10 blur intensity
        public float LightingAdjustment { get; set; } = 0f; // -1 to 1 brightness adjustment
        public bool EnableEdgeSmoothing { get; set; } = true;
        public bool PreserveLighting { get; set; } = true;
        public string? WatermarkText { get; set; }
    }

    public class ImageValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
        public bool HasDetectedSubject { get; set; }
        public float ConfidenceScore { get; set; }
    }
}