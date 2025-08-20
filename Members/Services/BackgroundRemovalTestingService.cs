namespace Members.Services
{
    public class BackgroundRemovalTestingService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<BackgroundRemovalTestingService> _logger;

        public BackgroundRemovalTestingService(
            IServiceProvider serviceProvider, 
            IWebHostEnvironment environment,
            ILogger<BackgroundRemovalTestingService> logger)
        {
            _serviceProvider = serviceProvider;
            _environment = environment;
            _logger = logger;
        }

        public async Task<List<BackgroundRemovalTestResult>> TestAllServicesAsync(string imagePath)
        {
            var results = new List<BackgroundRemovalTestResult>();
            
            // Get all available services
            var services = new List<IBackgroundRemovalService>
            {
                _serviceProvider.GetRequiredService<RemoveBgService>(),
                _serviceProvider.GetRequiredService<ClipdropService>()
                // Add more services here as you implement them
            };

            foreach (var service in services)
            {
                var testResult = await TestSingleServiceAsync(service, imagePath);
                results.Add(testResult);
            }

            return results;
        }

        public async Task<BackgroundRemovalTestResult> TestSingleServiceAsync(IBackgroundRemovalService service, string imagePath)
        {
            var testResult = new BackgroundRemovalTestResult
            {
                ServiceName = service.ServiceName
            };

            try
            {
                // Check if service is available
                var isAvailable = await service.IsServiceAvailableAsync();
                if (!isAvailable)
                {
                    testResult.Success = false;
                    testResult.Message = $"{service.ServiceName} is not available (check API key)";
                    return testResult;
                }

                // Get quota info
                var quotaInfo = await service.GetQuotaInfoAsync();
                testResult.TechnicalDetails["credits_remaining"] = quotaInfo.CreditsRemaining.ToString();

                // Process the image
                var result = await service.RemoveBackgroundAsync(imagePath);
                
                testResult.Success = result.Success;
                testResult.Message = result.Message;
                testResult.ProcessingTime = result.ProcessingTime;
                testResult.Cost = result.CostIncurred;

                if (result.Success && result.ProcessedImageBytes != null)
                {
                    // Save the result image for comparison
                    var fileName = $"{service.ServiceName}_{Path.GetFileNameWithoutExtension(imagePath)}_result.png";
                    var resultPath = Path.Combine(_environment.WebRootPath, "Images", "TestResults", fileName);
                    
                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
                    
                    await File.WriteAllBytesAsync(resultPath, result.ProcessedImageBytes);
                    testResult.ResultImagePath = $"/Images/TestResults/{fileName}";
                    
                    // Add technical details
                    foreach (var metadata in result.Metadata)
                    {
                        testResult.TechnicalDetails[metadata.Key] = metadata.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                testResult.Success = false;
                testResult.Message = $"Error testing {service.ServiceName}: {ex.Message}";
                _logger.LogError(ex, "Error testing service {ServiceName}", service.ServiceName);
            }

            return testResult;
        }

        public Task<string> GenerateComparisonReportAsync(List<BackgroundRemovalTestResult> results, string originalImagePath)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Background Removal Service Comparison Report ===");
            report.AppendLine($"Test Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Original Image: {Path.GetFileName(originalImagePath)}");
            report.AppendLine();

            var successfulResults = results.Where(r => r.Success).ToList();
            var failedResults = results.Where(r => !r.Success).ToList();

            if (successfulResults.Any())
            {
                report.AppendLine("✅ SUCCESSFUL SERVICES:");
                report.AppendLine();

                foreach (var result in successfulResults.OrderBy(r => r.Cost))
                {
                    report.AppendLine($"🔹 {result.ServiceName}");
                    report.AppendLine($"   Cost: ${result.Cost:F2}");
                    report.AppendLine($"   Processing Time: {result.ProcessingTime.TotalSeconds:F1}s");
                    report.AppendLine($"   Quality Rating: {result.QualityRating}/10 (manual rating needed)");
                    report.AppendLine($"   Result Image: {result.ResultImagePath}");
                    
                    if (result.TechnicalDetails.Any())
                    {
                        report.AppendLine("   Technical Details:");
                        foreach (var detail in result.TechnicalDetails)
                        {
                            report.AppendLine($"     - {detail.Key}: {detail.Value}");
                        }
                    }
                    report.AppendLine();
                }

                // Summary comparison
                report.AppendLine("📊 COMPARISON SUMMARY:");
                report.AppendLine($"   Cheapest: {successfulResults.OrderBy(r => r.Cost).First().ServiceName} (${successfulResults.Min(r => r.Cost):F2})");
                report.AppendLine($"   Fastest: {successfulResults.OrderBy(r => r.ProcessingTime).First().ServiceName} ({successfulResults.Min(r => r.ProcessingTime.TotalSeconds):F1}s)");
                report.AppendLine();
            }

            if (failedResults.Any())
            {
                report.AppendLine("❌ FAILED SERVICES:");
                report.AppendLine();
                
                foreach (var result in failedResults)
                {
                    report.AppendLine($"🔸 {result.ServiceName}");
                    report.AppendLine($"   Error: {result.Message}");
                    report.AppendLine();
                }
            }

            report.AppendLine("📝 RECOMMENDATIONS:");
            if (successfulResults.Count >= 2)
            {
                var cheapest = successfulResults.OrderBy(r => r.Cost).First();
                var fastest = successfulResults.OrderBy(r => r.ProcessingTime).First();
                
                report.AppendLine($"   - For cost efficiency: {cheapest.ServiceName} at ${cheapest.Cost:F2}/image");
                report.AppendLine($"   - For speed: {fastest.ServiceName} at {fastest.ProcessingTime.TotalSeconds:F1}s processing time");
                report.AppendLine("   - Quality comparison requires visual inspection of result images");
            }
            else if (successfulResults.Count == 1)
            {
                report.AppendLine($"   - Only {successfulResults.First().ServiceName} is currently working");
                report.AppendLine("   - Consider setting up additional services for comparison");
            }
            else
            {
                report.AppendLine("   - No services are currently working");
                report.AppendLine("   - Check API keys and service configurations");
            }

            return Task.FromResult(report.ToString());
        }
    }
}