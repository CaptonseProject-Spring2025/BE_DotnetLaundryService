using ClosedXML.Excel;
using LaundryService.Api.Services;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;


namespace LaundryService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExcelsController : ControllerBase
    {

          private readonly IExcelService _excelService;


        public ExcelsController(IExcelService excelsService)
        {
          _excelService = excelsService;
        }

            // Endpoint xuất file Excel
        [HttpGet("export-excel-users")]
        public async Task<IActionResult> ExportUsersToExcel()
        {
            // Gọi service để xuất file Excel
            var fileBytes = await _excelService.ExportUsersToExcel();

            // Trả về file Excel dưới dạng byte array
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Users.xlsx");
        }

         [HttpPost("import-excel-users")]
        public async Task<IActionResult> ImportUsersFromExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Không có file được tải lên.");
            }

            try
            {
                // Gọi service để xử lý file Excel và thêm người dùng vào cơ sở dữ liệu
                var users = await _excelService.ImportUsersFromExcel(file);

                return Ok(new { success = true, message = $"{users.Count} users đã được thêm vào thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }    
        
       
    }
}
