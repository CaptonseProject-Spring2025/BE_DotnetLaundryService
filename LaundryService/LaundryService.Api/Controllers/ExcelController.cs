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
        [HttpGet("export")]
        public async Task<IActionResult> ExportUsersToExcel()
        {
            // Gọi service để xuất file Excel
            var fileBytes = await _excelService.ExportUsersToExcel();

            // Trả về file Excel dưới dạng byte array
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Users.xlsx");
        }
       
    }
}
