using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using SkiaSharp;

namespace HealthRecordSummFunc
{
    public class SplitFunction
    {
        private readonly ILogger<SplitFunction> _logger;

        public SplitFunction(ILogger<SplitFunction> logger)
        {
            _logger = logger;
        }

        [Function("Split")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            [Microsoft.Azure.Functions.Worker.Http.FromBody] BlobRequest blobRequest)
        {
            // Validate that the request body included a blob path.
            string blobPath = blobRequest.BlobPath;
            _logger.LogInformation($"Processing {blobPath}");

            if (string.IsNullOrWhiteSpace(blobPath))
            {
                _logger.LogError("BlobPath is required");
                return new BadRequestResult();
            }

            // Extract the file name from the blob path.
            string fileName = blobPath.Substring(blobPath.LastIndexOf('/') + 1);

            // Get the Storage Account connection string from the environment.
            string storageAccountConnectionString = System.Environment.GetEnvironmentVariable("StorageAccountConnectionString");

            if (string.IsNullOrWhiteSpace(storageAccountConnectionString))
            {
                _logger.LogError("Could not retrieve the Storage Account connection string");
                return new StatusCodeResult(500);
            }

            // Get the input blob client.
            var blobServiceClient = new BlobServiceClient(storageAccountConnectionString);

            var inputBlobContainerClient = blobServiceClient.GetBlobContainerClient("intake");
            var inputBlobClient = inputBlobContainerClient.GetBlobClient(fileName);

            var outputBlobContainerClient = blobServiceClient.GetBlobContainerClient("output");

            // Download the PDF blob.
            using MemoryStream inputFileStream = new MemoryStream();
            inputBlobClient.DownloadTo(inputFileStream);

            // Convert the PDF to individual images for each page.
            var bitmaps = PDFtoImage.Conversion.ToImages(inputFileStream);

            int counter = 1;
            List<BlobResponse> outputFilePaths = new();
            
            foreach (SKBitmap bitmap in bitmaps)
            {
                string outputFileNumber = counter++.ToString("000");
                string outputFileName = fileName.Replace(".pdf", $"-{outputFileNumber}.jpg");
                
                _logger.LogInformation($"Writing output file {outputFileName}");

                using var outputFileStream = new MemoryStream();
                using var image = SKImage.FromBitmap(bitmap);
                using var encodedImage = image.Encode(SKEncodedImageFormat.Jpeg, 100);

                encodedImage.SaveTo(outputFileStream);

                outputFileStream.Position = 0;
                var outputBlobClient = outputBlobContainerClient.GetBlobClient(outputFileName);

                await outputBlobClient.DeleteIfExistsAsync();
                await outputBlobClient.UploadAsync(outputFileStream);
                
                outputFilePaths.Add(new BlobResponse(outputBlobClient.Uri.ToString()));
            }

            string jsonOutputFilePaths = JsonSerializer.Serialize(outputFilePaths);
            return new OkObjectResult(jsonOutputFilePaths);
        }
    }

    public class BlobRequest
    {
        public string BlobPath {  get; set; } 
    }

    public class BlobResponse(string filePath)
    {
        public string filepath { get; set; } = filePath;
    }
}
