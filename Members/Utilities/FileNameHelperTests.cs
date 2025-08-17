using Members.Utilities;

namespace Members.Tests.Utilities
{
    public class FileNameHelperTests
    {
        public static void RunTests()
        {
            TestCreateSafeFileName();
            TestCreateSafeUrlPath();
            TestSanitizeFileName();
            TestShortenFileName();
            Console.WriteLine("All FileNameHelper tests passed!");
        }

        private static void TestCreateSafeFileName()
        {
            // Test normal filename
            var result1 = FileNameHelper.CreateSafeFileName("test.jpg");
            Assert(result1.Length <= 100, $"Normal filename too long: {result1.Length}");
            Assert(result1.Contains("test"), "Should contain original name");
            Assert(result1.EndsWith(".jpg"), "Should preserve extension");

            // Test very long filename
            var longName = new string('a', 200) + ".jpg";
            var result2 = FileNameHelper.CreateSafeFileName(longName);
            Assert(result2.Length <= 100, $"Long filename not truncated: {result2.Length}");
            Assert(result2.EndsWith(".jpg"), "Should preserve extension even when truncating");

            // Test filename with invalid characters
            var invalidName = "test<>:\"\\|?*.jpg";
            var result3 = FileNameHelper.CreateSafeFileName(invalidName);
            Assert(!result3.Contains("<"), "Should remove invalid characters");
            Assert(!result3.Contains(">"), "Should remove invalid characters");
            Assert(result3.EndsWith(".jpg"), "Should preserve extension");

            // Test empty filename
            var result4 = FileNameHelper.CreateSafeFileName("");
            Assert(result4.Length > 0, "Should handle empty filename");
            Assert(result4.Contains(".jpg"), "Should provide default extension");
        }

        private static void TestCreateSafeUrlPath()
        {
            var basePath = "/Images/Backgrounds/";
            var fileName = "test.jpg";
            var result = FileNameHelper.CreateSafeUrlPath(basePath, fileName);
            Assert(result.StartsWith("/Images/Backgrounds/"), "Should preserve base path");
            Assert(result.EndsWith("test.jpg"), "Should preserve filename");
            Assert(result.Length <= 200, "Should respect path length limits");
        }

        private static void TestSanitizeFileName()
        {
            var result1 = FileNameHelper.SanitizeFileName("test<>file");
            Assert(!result1.Contains("<"), "Should remove invalid characters");
            Assert(!result1.Contains(">"), "Should remove invalid characters");
            Assert(result1.Contains("test"), "Should preserve valid characters");

            var result2 = FileNameHelper.SanitizeFileName("");
            Assert(result2 == "unnamed", "Should handle empty input");
        }

        private static void TestShortenFileName()
        {
            var longFileName = new string('a', 150) + ".jpg";
            var result = FileNameHelper.ShortenFileName(longFileName, 50);
            Assert(result.Length <= 50, $"Filename not shortened: {result.Length}");
            Assert(result.EndsWith(".jpg"), "Should preserve extension");

            var shortFileName = "short.jpg";
            var result2 = FileNameHelper.ShortenFileName(shortFileName, 50);
            Assert(result2 == shortFileName, "Should not modify already short filenames");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Test failed: {message}");
            }
        }
    }
}

// TODO: Remove this test file before deployment - it's just for development testing