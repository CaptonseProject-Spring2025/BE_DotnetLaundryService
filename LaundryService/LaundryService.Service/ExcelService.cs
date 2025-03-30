using ClosedXML.Excel;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LaundryService.Api.Services
{
  public class ExcelsService : IExcelService
  {
    private readonly IUnitOfWork _unitOfWork;

    public ExcelsService(IUnitOfWork unitOfWork)
    {
      _unitOfWork = unitOfWork;
    }

    public async Task<byte[]> ExportUsersToExcel()
    {
      // Lấy danh sách người dùng từ CSDL
      var users = await _unitOfWork.Repository<User>().GetAllAsync();

      // Tạo một Workbook mới
      var workbook = new XLWorkbook();
      var worksheet = workbook.Worksheets.Add("Users");

      
      // Tạo header cho các cột
      var headerRow = worksheet.Row(1); // Lấy dòng header

      // Tạo header cho các cột
      worksheet.Cell(1, 1).Value = "UserId";
      worksheet.Cell(1, 2).Value = "Họ tên";
      worksheet.Cell(1, 3).Value = "Trạng thái";
      worksheet.Cell(1, 4).Value = "Vai trò";
      worksheet.Cell(1, 5).Value = "Hình ảnh";
      worksheet.Cell(1, 6).Value = "Ngày sinh";
      worksheet.Cell(1, 7).Value = "Giới tính";
      worksheet.Cell(1, 8).Value = "Số điện thoại";

       // Thiết lập màu nền cho header
      headerRow.Style.Fill.BackgroundColor = XLColor.CornflowerBlue; // Màu nền xanh dương
      headerRow.Style.Font.FontColor = XLColor.White; // Màu chữ trắng
      headerRow.Style.Font.Bold = true; // Đặt chữ in đậm
      headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; // Căn giữa  

      // Điền dữ liệu vào các cột
      int row = 2;
      foreach (var user in users)
      {
        worksheet.Cell(row, 1).Value = user?.Userid.ToString();
        worksheet.Cell(row, 2).Value = user?.Fullname;
        worksheet.Cell(row, 3).Value = user?.Status;
        worksheet.Cell(row, 4).Value = user?.Role;
        worksheet.Cell(row, 5).Value = user?.Avatar ?? "Chưa có hình ảnh"; // Nếu không có hình ảnh thì hiển thị "Chưa có hình ảnh"
        worksheet.Cell(row, 6).Value = user?.Dob?.ToString("yyyy-MM-dd"); // Nếu có DOB
        worksheet.Cell(row, 7).Value = user?.Gender;
        worksheet.Cell(row, 8).Value = user?.Phonenumber;
        row++;
      }

      // Đặt định dạng cho cột
      worksheet.Columns().AdjustToContents();

      // Trả về file Excel dưới dạng byte array
      using (var stream = new MemoryStream())
      {
        workbook.SaveAs(stream);
        return stream.ToArray();
      }
    }
  }
}
