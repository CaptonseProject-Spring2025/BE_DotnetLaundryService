using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace LaundryService.Api.Controllers
{
    [Route("api/storage")]
    [ApiController]
    public class StorageController : BaseApiController
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<StorageController> _logger;

        // Inject IFileStorageService (sẽ được DI container cung cấp B2StorageService)
        public StorageController(IFileStorageService fileStorageService, ILogger<StorageController> logger)
        {
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        /// <summary>
        /// Tải lên một file vào thư mục chỉ định trên B2 Storage.
        /// </summary>
        /// <param name="file">File cần tải lên (multipart/form-data).</param>
        /// <param name="folderName">Tên thư mục trong bucket để lưu file (ví dụ: 'avatars', 'product-images').</param>
        /// <returns>URL công khai của file đã tải lên.</returns>
        /// <response code="200">Tải lên thành công, trả về URL.</response>
        /// <response code="400">File không hợp lệ hoặc thiếu tên thư mục.</response>
        /// <response code="401">Chưa xác thực (nếu [Authorize] được bật).</response>
        /// <response code="403">Không có quyền (nếu yêu cầu role cụ thể).</response>
        /// <response code="500">Lỗi server trong quá trình tải lên.</response>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public async Task<IActionResult> UploadFile(
            [Required] IFormFile file, // Đánh dấu file là bắt buộc
            [FromQuery, Required] string folderName) // Lấy folderName từ query string và đánh dấu bắt buộc
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return BadRequest(new { Message = "Folder name is required." });
            }

            // Kiểm tra file (dù service cũng kiểm tra nhưng kiểm tra sớm tốt hơn)
            if (file == null || file.Length == 0)
            {
                _logger.LogError("run fail: {file}", file);
                return BadRequest(new { Message = "Invalid file provided." });
            }

            try
            {
                // Gọi service để tải file lên
                var fileUrl = await _fileStorageService.UploadFileAsync(file, folderName);
                // Trả về URL nếu thành công
                return Ok(new { Url = fileUrl });
            }
            catch (ArgumentException ex) // Bắt lỗi cụ thể từ service
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (ApplicationException ex) // Bắt lỗi ApplicationException (ví dụ: lỗi upload từ service)
            {
                _logger.LogError("B2 Upload Error: {ex}" , ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Error uploading file to storage." });
            }
            catch (Exception ex) // Bắt các lỗi không mong muốn khác
            {
                _logger.LogError("Unexpected Upload Error: {ex}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An unexpected error occurred during file upload." });
            }
        }

        /// <summary>
        /// Xóa một file khỏi B2 Storage dựa vào URL của nó.
        /// </summary>
        /// <param name="fileUrl">URL đầy đủ của file cần xóa.</param>
        /// <returns>Thông báo thành công hoặc lỗi.</returns>
        /// <response code="200">Xóa thành công (hoặc file không tồn tại và không có lỗi).</response>
        /// <response code="400">URL file không hợp lệ hoặc bị thiếu.</response>
        /// <response code="401">Chưa xác thực (nếu [Authorize] được bật).</response>
        /// <response code="403">Không có quyền (nếu yêu cầu role cụ thể).</response>
        /// <response code="500">Lỗi server trong quá trình xóa.</response>
        [HttpDelete("delete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteFile([FromQuery, Required] string fileUrl) // Lấy URL từ query string
        {
            if (string.IsNullOrWhiteSpace(fileUrl) || !Uri.TryCreate(fileUrl, UriKind.Absolute, out _))
            {
                return BadRequest(new { Message = "Invalid or missing file URL." });
            }

            try
            {
                // Gọi service để xóa file
                await _fileStorageService.DeleteFileAsync(fileUrl);

                // Lưu ý: Service hiện tại đang bắt lỗi bên trong và chỉ log ra Console.
                // Do đó, trừ khi có lỗi xảy ra *trước* khi gọi DeleteFileAsync hoặc
                // *bên trong* DeleteFileAsync mà *không* bị bắt, action này sẽ
                // thường trả về 200 OK ngay cả khi việc xóa trên B2 có vấn đề
                // (và chỉ được log lại bởi service).
                // Nếu muốn controller nhận biết lỗi xóa, bạn cần sửa đổi DeleteFileAsync
                // trong service để nó ném (throw) exception hoặc trả về một trạng thái (bool).

                return Ok(new { Message = "File deletion request processed." });
            }
            catch (Exception ex) // Bắt các lỗi không mong muốn (ví dụ lỗi mạng trước khi gọi service)
            {
                _logger.LogError(ex, "An unexpected error occurred during file deletion for URL: {FileUrl}", fileUrl); // Log lỗi với Serilog
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An unexpected error occurred during file deletion." });
            }
        }

        /// <summary>
        /// Tải lên nhiều files vào thư mục chỉ định trên B2 Storage.
        /// </summary>
        /// <param name="files">Danh sách các files cần tải lên (multipart/form-data).</param>
        /// <param name="folderName">Tên thư mục trong bucket để lưu các files.</param>
        /// <returns>Kết quả chi tiết về các files tải lên thành công và thất bại.</returns>
        /// <response code="200">Xử lý hoàn tất, trả về danh sách thành công và thất bại.</response>
        /// <response code="400">Thiếu files, thiếu tên thư mục hoặc dữ liệu không hợp lệ.</response>
        /// <response code="500">Lỗi server trong quá trình xử lý.</response>
        [HttpPost("upload-multiple")]
        // Use FromForm for IFormFileCollection
        [ProducesResponseType(typeof(UploadMultipleFilesResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> UploadMultipleFiles(
            [FromForm] IFormFileCollection files, // Use IFormFileCollection
            [FromQuery, Required] string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return BadRequest(new { Message = "Folder name is required." });
            }

            if (files == null || files.Count == 0)
            {
                _logger.LogWarning("UploadMultipleFiles endpoint called with no files.");
                return BadRequest(new { Message = "No files provided for upload." });
            }

            // Optional: Add validation for total size, individual file sizes/types, or file count limit here
            long totalSize = files.Sum(f => f.Length);
            _logger.LogInformation("Received {FileCount} files for multiple upload. Total size: {TotalSize} bytes. Folder: {Folder}", files.Count, totalSize, folderName);


            try
            {
                // Call the service method
                var result = await _fileStorageService.UploadMultipleFilesAsync(files, folderName);

                // Return the detailed result object
                // Status 200 OK indicates the *process* completed, even if some files failed.
                // The client needs to inspect the response body for details.
                return Ok(result);
            }
            catch (ArgumentException ex) // Catch specific argument errors from service (e.g., empty folder name)
            {
                _logger.LogWarning("UploadMultipleFiles argument error: {ErrorMessage}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex) // Catch unexpected errors during the service call setup or Task.WhenAll
            {
                _logger.LogError(ex, "Unexpected error during multiple file upload process.");
                // Return a generic server error, as individual file errors are handled in the service and returned in the result DTO.
                // If an exception happens here, it's likely before or after individual uploads were attempted/logged.
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An unexpected error occurred while processing the multiple file upload request." });
            }
        }
    }
}
