namespace Members.Services
{
    public interface IImageAnalysisService
    {
        Task<string> GenerateDescriptiveFilenameAsync(string imagePath);
        Task<ImageAnalysisResult> AnalyzeImageAsync(string imagePath);
        Task<List<string>> GenerateBatchFilenamesAsync(List<string> imagePaths);
    }

    public class ImageAnalysisResult
    {
        public string SuggestedFilename { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public float Confidence { get; set; }
        public string OriginalPath { get; set; } = string.Empty;
    }
}