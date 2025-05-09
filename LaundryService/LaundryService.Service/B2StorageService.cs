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
using Microsoft.Extensions.Logging;
using LaundryService.Dto.Responses;
using MimeKit;

namespace LaundryService.Service
{
    public class B2StorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _baseUrl;
        private readonly ILogger<B2StorageService> _logger;
        private readonly TransferUtility _transferUtility;

        public B2StorageService(IConfiguration configuration, ILogger<B2StorageService> logger)
        {
            _logger = logger;

            // Get configuration values
            var accessKey = configuration["BackblazeB2:KeyId"];
            var secretKey = configuration["BackblazeB2:ApplicationKey"];
            var endpoint = configuration["BackblazeB2:Endpoint"];
            _bucketName = configuration["BackblazeB2:BucketName"];
            _baseUrl = configuration["BackblazeB2:BaseUrl"];

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(_bucketName) || string.IsNullOrEmpty(_baseUrl))
            {
                _logger.LogError("Backblaze B2 configuration is incomplete. Please check KeyId, ApplicationKey, Endpoint, BucketName, and BaseUrl.");
                throw new ArgumentException("Backblaze B2 configuration is incomplete.");
            }

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
            _transferUtility = new TransferUtility(_s3Client); // <--- Initialize TransferUtility

            _logger.LogInformation("B2StorageService initialized with Endpoint: {Endpoint}, Bucket: {BucketName}, SignatureVersion: {SignatureVersion}",
                                   endpoint, _bucketName, s3Config.SignatureVersion);
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Invalid file");

            // Create a unique file name
            var fileExtension = Path.GetExtension(file.FileName);
            // Cân nhắc làm sạch fileExtension để tránh lỗ hổng bảo mật
            fileExtension = fileExtension?.ToLowerInvariant(); // ví dụ: chuyển về chữ thường
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            // Cân nhắc làm sạch folderName
            var key = $"{folderName}/{fileName}";

            _logger.LogInformation("Attempting to upload file. Key: {Key}, ContentType: {ContentType}, Size: {Size}", key, file.ContentType, file.Length);

            try
            {
                // Use the file's stream directly
                using (var fileStream = file.OpenReadStream())
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = key,
                        InputStream = fileStream,// Pass the file stream directly
                        ContentType = file.ContentType,
                        CannedACL = S3CannedACL.PublicRead
                        // You might need to set the ContentLength if the SDK doesn't do it automatically
                        // from a non-seekable stream, although often it handles it.
                        // Check SDK documentation or test if needed.
                        // ContentLength = file.Length
                    };

                    await _s3Client.PutObjectAsync(putRequest);
                }
            }
            catch (AmazonS3Exception s3Ex)
            {
                _logger.LogError(s3Ex, "AWS S3 Error uploading file to B2. Key: {Key}, AWS ErrorCode: {ErrorCode}, AWS ErrorType: {ErrorType}, HttpStatusCode: {StatusCode}",
                                 key, s3Ex.ErrorCode, s3Ex.ErrorType, s3Ex.StatusCode);
                // Ném lại lỗi cụ thể hơn nếu cần phân biệt lỗi S3
                throw new ApplicationException($"S3 Error uploading file to B2: {s3Ex.Message} (Code: {s3Ex.ErrorCode})", s3Ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error uploading file to B2. Key: {Key}", key);
                throw new ApplicationException($"Error uploading file to B2: {ex.Message}", ex);
            }

            // Return the URL to the uploaded file
            return $"{_baseUrl}/{key}";
        }

        public async Task<UploadMultipleFilesResult> UploadMultipleFilesAsync(IFormFileCollection files, string folderName)
        {
            if (files == null || files.Count == 0)
            {
                _logger.LogWarning("UploadMultipleFilesAsync called with no files.");
                // Returning an empty result might be better than throwing here
                // throw new ArgumentException("No files provided for upload.");
                return new UploadMultipleFilesResult();
            }
            if (string.IsNullOrWhiteSpace(folderName))
            {
                _logger.LogError("UploadMultipleFilesAsync called with empty folder name.");
                throw new ArgumentException("Folder name cannot be empty.", nameof(folderName));
            }

            _logger.LogInformation("Starting multiple file upload. FileCount: {FileCount}, Folder: {Folder}", files.Count, folderName);

            var result = new UploadMultipleFilesResult();
            var uploadTasks = new List<Task>(); // Store tasks for concurrent execution

            // --- IMPORTANT: Process files concurrently ---
            foreach (var file in files)
            {
                // Create a task for each file upload
                uploadTasks.Add(Task.Run(async () => // Use Task.Run for CPU-bound parts like Guid generation and potentially stream handling, but the core is I/O bound (UploadAsync)
                {
                    if (file == null || file.Length == 0)
                    {
                        _logger.LogWarning("Skipping invalid file entry in collection: {FileName}", file?.FileName ?? "N/A");
                        // Optionally add to failed list here if desired
                        // lock (result.FailedUploads) // Protect list access if modifying directly here
                        // {
                        //     result.FailedUploads.Add(new FailedUploadInfo { OriginalFileName = file?.FileName ?? "N/A", ErrorMessage = "Invalid or empty file data" });
                        // }
                        return; // Skip this file
                    }

                    var originalFileName = file.FileName; // Store before potential exceptions
                    string key = ""; // Declare key outside try for logging in catch

                    try
                    {
                        // Create unique key within the specified folder
                        var fileExtension = Path.GetExtension(originalFileName)?.ToLowerInvariant();
                        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                        key = $"{folderName}/{uniqueFileName}"; // Consider sanitizing folderName and uniqueFileName

                        _logger.LogDebug("Starting upload for: {OriginalFileName}, Key: {Key}, Size: {Size}", originalFileName, key, file.Length);

                        using (var fileStream = file.OpenReadStream())
                        {
                            var uploadRequest = new TransferUtilityUploadRequest
                            {
                                BucketName = _bucketName,
                                Key = key,
                                InputStream = fileStream,
                                ContentType = file.ContentType, // Use the file's content type
                                CannedACL = S3CannedACL.PublicRead // Make file publicly readable
                                                                   // TransferUtility handles ContentLength and multipart logic
                            };

                            await _transferUtility.UploadAsync(uploadRequest);
                        }

                        // If successful, add to the success list
                        var fileUrl = $"{_baseUrl}/{key}";
                        _logger.LogInformation("Successfully uploaded: {OriginalFileName} to {Url}", originalFileName, fileUrl);
                        lock (result.SuccessfulUploads) // Lock to prevent race conditions when adding to the list from multiple threads
                        {
                            result.SuccessfulUploads.Add(new SuccessfulUploadInfo
                            {
                                OriginalFileName = originalFileName,
                                Url = fileUrl
                            });
                        }
                    }
                    catch (AmazonS3Exception s3Ex)
                    {
                        _logger.LogError(s3Ex, "AWS S3 Error uploading file {OriginalFileName} to B2. Key: {Key}, AWS ErrorCode: {ErrorCode}, AWS ErrorType: {ErrorType}, HttpStatusCode: {StatusCode}",
                                         originalFileName, key, s3Ex.ErrorCode, s3Ex.ErrorType, s3Ex.StatusCode);
                        lock (result.FailedUploads) // Lock to prevent race conditions
                        {
                            result.FailedUploads.Add(new FailedUploadInfo
                            {
                                OriginalFileName = originalFileName,
                                ErrorMessage = $"S3 Error: {s3Ex.Message} (Code: {s3Ex.ErrorCode})"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Generic error uploading file {OriginalFileName} to B2. Key: {Key}", originalFileName, key);
                        lock (result.FailedUploads) // Lock to prevent race conditions
                        {
                            result.FailedUploads.Add(new FailedUploadInfo
                            {
                                OriginalFileName = originalFileName,
                                ErrorMessage = $"Generic Error: {ex.Message}"
                            });
                        }
                    }
                }));
            }

            // Wait for all upload tasks to complete
            try
            {
                await Task.WhenAll(uploadTasks);
                _logger.LogInformation("Finished multiple file upload process. Success: {SuccessCount}, Failed: {FailureCount}", result.SuccessCount, result.FailureCount);
            }
            catch (Exception ex)
            {
                // This catch block is primarily for issues with Task.WhenAll itself,
                // individual file exceptions are handled within the Task.Run lambda.
                _logger.LogError(ex, "Error occurred while waiting for multiple uploads to complete.");
                // Potentially add a general failure note if needed, though individual errors are logged/captured.
            }


            return result;
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
            catch (AmazonS3Exception s3Ex)
            {
                // Bắt lỗi cụ thể từ S3
                _logger.LogError(s3Ex, "AWS S3 Error deleting file from B2. AWS ErrorCode: {ErrorCode}, AWS ErrorType: {ErrorType}, HttpStatusCode: {StatusCode}",
                                 s3Ex.ErrorCode, s3Ex.ErrorType, s3Ex.StatusCode);
                // Quyết định xem có nên ném lỗi ra ngoài không.
                // Hiện tại đang "nuốt" lỗi, chỉ log lại.
                // throw new ApplicationException($"S3 Error deleting file: {s3Ex.Message}", s3Ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error deleting file from B2.");
                // Quyết định xem có nên ném lỗi ra ngoài không.
                // throw new ApplicationException($"Error deleting file: {ex.Message}", ex);
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

        public async Task<string> UploadStreamAsync(Stream stream, string folder, string ext)
        {
            var key = $"{folder}/{Guid.NewGuid()}{ext}";
            var put = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = MimeTypes.GetMimeType(ext),
                CannedACL = S3CannedACL.PublicRead
            };
            await _s3Client.PutObjectAsync(put);
            return $"{_baseUrl}/{key}";
        }
    }
}
