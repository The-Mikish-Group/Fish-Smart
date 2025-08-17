using System.Text;

namespace Members.Utilities
{
    public static class FileNameHelper
    {
        // Windows path limit is 260 characters, but we want a safety margin
        private const int MAX_FILENAME_LENGTH = 100;
        private const int MAX_PATH_LENGTH = 200;
        
        /// <summary>
        /// Creates a safe, unique filename that won't exceed filesystem limits
        /// </summary>
        /// <param name="originalFileName">The original uploaded filename</param>
        /// <param name="includeGuid">Whether to include a GUID for uniqueness (default: true)</param>
        /// <returns>A safe filename that won't cause "path too long" errors</returns>
        public static string CreateSafeFileName(string originalFileName, bool includeGuid = true)
        {
            if (string.IsNullOrEmpty(originalFileName))
                return includeGuid ? $"{Guid.NewGuid()}.jpg" : "unnamed.jpg";

            // Get the file extension
            var extension = Path.GetExtension(originalFileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            
            // Clean the filename of invalid characters
            var cleanName = SanitizeFileName(nameWithoutExtension);
            
            // Calculate available space for the name part
            var guidLength = includeGuid ? 37 : 0; // GUID + underscore = 37 chars
            var extensionLength = extension.Length;
            var maxNameLength = MAX_FILENAME_LENGTH - guidLength - extensionLength;
            
            // Truncate if necessary
            if (cleanName.Length > maxNameLength)
            {
                cleanName = cleanName.Substring(0, Math.Max(1, maxNameLength));
            }
            
            // Build the final filename
            if (includeGuid)
            {
                return $"{Guid.NewGuid()}_{cleanName}{extension}";
            }
            else
            {
                return $"{cleanName}{extension}";
            }
        }
        
        /// <summary>
        /// Creates a web-safe URL path that won't exceed limits
        /// </summary>
        /// <param name="basePath">Base path like "/Images/Backgrounds/"</param>
        /// <param name="fileName">The filename</param>
        /// <returns>Complete URL path, truncated if necessary</returns>
        public static string CreateSafeUrlPath(string basePath, string fileName)
        {
            var fullPath = $"{basePath.TrimEnd('/')}/{fileName}";
            
            if (fullPath.Length > MAX_PATH_LENGTH)
            {
                // If the full path is too long, create a shorter filename
                var extension = Path.GetExtension(fileName);
                var availableLength = MAX_PATH_LENGTH - basePath.Length - extension.Length - 1;
                var shortName = fileName.Substring(0, Math.Max(1, availableLength - extension.Length)) + extension;
                fullPath = $"{basePath.TrimEnd('/')}/{shortName}";
            }
            
            return fullPath;
        }
        
        /// <summary>
        /// Removes invalid filename characters and replaces them with safe alternatives
        /// </summary>
        /// <param name="fileName">The filename to sanitize</param>
        /// <returns>A sanitized filename</returns>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "unnamed";
                
            var invalidChars = Path.GetInvalidFileNameChars();
            var stringBuilder = new StringBuilder();
            
            foreach (char c in fileName)
            {
                if (invalidChars.Contains(c))
                {
                    stringBuilder.Append('_');
                }
                else if (char.IsControl(c))
                {
                    stringBuilder.Append('_');
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }
            
            var result = stringBuilder.ToString();
            
            // Remove multiple consecutive underscores
            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }
            
            // Trim underscores from start and end
            result = result.Trim('_');
            
            // Ensure we have at least something
            if (string.IsNullOrEmpty(result))
                result = "unnamed";
                
            return result;
        }
        
        /// <summary>
        /// Checks if a filename/path might cause filesystem issues
        /// </summary>
        /// <param name="filePath">The file path to check</param>
        /// <returns>True if the path is safe, false if it might cause issues</returns>
        public static bool IsPathSafe(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
                
            // Check total length
            if (filePath.Length > MAX_PATH_LENGTH)
                return false;
                
            // Check for invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (filePath.Any(c => invalidChars.Contains(c)))
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Creates a shortened version of an existing long filename while preserving the extension
        /// </summary>
        /// <param name="longFileName">The filename that's too long</param>
        /// <param name="maxLength">Maximum allowed length (default: 100)</param>
        /// <returns>A shortened filename</returns>
        public static string ShortenFileName(string longFileName, int maxLength = MAX_FILENAME_LENGTH)
        {
            if (string.IsNullOrEmpty(longFileName) || longFileName.Length <= maxLength)
                return longFileName;
                
            var extension = Path.GetExtension(longFileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(longFileName);
            
            var maxNameLength = maxLength - extension.Length;
            
            if (maxNameLength <= 0)
                return $"file{extension}";
                
            var shortenedName = nameWithoutExtension.Substring(0, Math.Min(nameWithoutExtension.Length, maxNameLength));
            return $"{shortenedName}{extension}";
        }
    }
}