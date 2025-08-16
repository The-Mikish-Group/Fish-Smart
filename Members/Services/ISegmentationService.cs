namespace Members.Services
{
    public interface ISegmentationService
    {
        /// <summary>
        /// Generates a high-quality segmentation mask using AI models
        /// </summary>
        /// <param name="imagePath">Path to the image to segment</param>
        /// <returns>Segmentation mask as byte array (PNG format)</returns>
        Task<byte[]?> GenerateSegmentationMaskAsync(string imagePath);

        /// <summary>
        /// Checks if AI segmentation models are available
        /// </summary>
        /// <returns>True if AI models are loaded and ready</returns>
        bool IsAISegmentationAvailable();

        /// <summary>
        /// Downloads and initializes AI segmentation models
        /// </summary>
        /// <returns>Success status</returns>
        Task<bool> InitializeAIModelsAsync();
    }

    public enum SegmentationMethod
    {
        ColorBased,    // Current basic method
        AI_U2Net,      // U2-Net model for portrait segmentation
        AI_DeepLab,    // DeepLab v3+ for general segmentation
        CloudAPI       // Azure/Google Cloud Vision API
    }
}