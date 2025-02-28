using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3;
using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class B2StorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _baseUrl;

        public B2StorageService(IConfiguration configuration)
        {
            // Get configuration values
            var accessKey = configuration["BackblazeB2:KeyId"];
            var secretKey = configuration["BackblazeB2:ApplicationKey"];
            var endpoint = configuration["BackblazeB2:Endpoint"];
            _bucketName = configuration["BackblazeB2:BucketName"];
            _baseUrl = configuration["BackblazeB2:BaseUrl"];

            // Create S3 client with Backblaze B2 configuration
            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true, // Required for Backblaze B2
                UseHttp = false,
                DisableHostPrefixInjection = true,
                //add this to prevent "Unsupported header 'x-amz-sdk-checksum-algorithm' received for this API call." error
                SignatureVersion = "2", // Use older signature version
            };

            _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Invalid file");

            // Create a unique file name
            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var key = $"{folderName}/{fileName}";

            try
            {
                // Upload file to B2 using PutObjectRequest instead of TransferUtility
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = key,
                        InputStream = memoryStream,
                        ContentType = file.ContentType,
                        CannedACL = S3CannedACL.PublicRead
                    };

                    await _s3Client.PutObjectAsync(putRequest);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error uploading file to B2: {ex.Message}", ex);
            }

            // Return the URL to the uploaded file
            return $"{_baseUrl}/{key}";
        }

        public async Task DeleteFileAsync(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return;

            try
            {
                // Extract the key from the URL
                var key = GetKeyFromUrl(fileUrl);
                if (string.IsNullOrEmpty(key))
                    return;

                // Tìm file version để xóa hoàn toàn
                var listRequest = new ListVersionsRequest
                {
                    BucketName = _bucketName,
                    Prefix = key
                };

                var listResponse = await _s3Client.ListVersionsAsync(listRequest);
                var fileVersion = listResponse.Versions.FirstOrDefault();

                if (fileVersion != null)
                {
                    var deleteRequest = new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = key,
                        VersionId = fileVersion.VersionId // Xóa theo phiên bản file
                    };

                    await _s3Client.DeleteObjectAsync(deleteRequest);
                }

                //This code is used to delete but it hides the file instead of deleting it
                // Delete the file from B2
                //var deleteRequest = new DeleteObjectRequest
                //{
                //    BucketName = _bucketName,
                //    Key = key
                //};

                //await _s3Client.DeleteObjectAsync(deleteRequest);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw exception
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }

        private string GetKeyFromUrl(string fileUrl)
        {
            if (fileUrl.StartsWith(_baseUrl))
            {
                return fileUrl.Substring(_baseUrl.Length + 1); // +1 for the / character
            }

            return fileUrl; // Return as is if it's already a key
        }
    }
}
