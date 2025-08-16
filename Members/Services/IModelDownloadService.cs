namespace Members.Services
{
    public interface IModelDownloadService
    {
        Task<bool> DownloadU2NetModelAsync(string destinationPath);
        Task<bool> IsModelAvailableAsync(string modelPath);
        Task<string> GetModelInfoAsync(string modelPath);
        Task<bool> VerifyModelIntegrityAsync(string modelPath);
    }
}