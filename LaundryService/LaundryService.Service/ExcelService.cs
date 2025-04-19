using ClosedXML.Excel;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
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
        worksheet.Cell(row, 1).Value = user?.Userid.ToString() ?? "Chưa có mã người dùng"; // Nếu không có mã người dùng thì hiển thị "Chưa có mã người dùng"
        worksheet.Cell(row, 2).Value = user?.Fullname ?? "Chưa có họ tên"; // Nếu không có họ tên thì hiển thị "Chưa có họ tên"
        worksheet.Cell(row, 3).Value = user?.Status ?? "Chưa có trạng thái"; // Nếu không có trạng thái thì hiển thị "Chưa có trạng thái"
        worksheet.Cell(row, 4).Value = user?.Role ?? "Chưa có vai trò"; // Nếu không có vai trò thì hiển thị "Chưa có vai trò"
        worksheet.Cell(row, 5).Value = user?.Avatar ?? "Chưa có hình ảnh"; // Nếu không có hình ảnh thì hiển thị "Chưa có hình ảnh"
        worksheet.Cell(row, 6).Value = user?.Dob?.ToString("yyyy-MM-dd") ?? "Chưa có ngày sinh"; // Nếu có DOB
        worksheet.Cell(row, 7).Value = user?.Gender ?? "Chưa có giới tính"; // Nếu không có giới tính thì hiển thị "Chưa có giới tính"
        worksheet.Cell(row, 8).Value = user?.Phonenumber ?? "Chưa có số điện thoại"; // Nếu không có số điện thoại thì hiển thị "Chưa có số điện thoại"
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

    public async Task<List<User>> ImportUsersFromExcel(IFormFile file)
    {
      var users = new List<User>();
      int i = 1; // Biến đếm số điện thoại UNKNOWN

      using (var stream = new MemoryStream())
      {
        await file.CopyToAsync(stream);

        using (var workbook = new XLWorkbook(stream))
        {
          var worksheet = workbook.Worksheets.Worksheet(1); // Lấy sheet đầu tiên

          var rowCount = worksheet.RowsUsed().Count(); // Lấy số dòng đã sử dụng

          for (int row = 2; row <= rowCount; row++) // Bắt đầu từ dòng 2 trở đi
          {
            var phoneNumber = worksheet.Cell(row, 8).Value.ToString();

            // Nếu số điện thoại trống, gán giá trị UNKNOWN-{i}
            if (string.IsNullOrEmpty(phoneNumber))
            {
              phoneNumber = $"UNKNOWN-{i++}"; // Tăng i sau khi gán
            }

            var user = new User
            {
              Fullname = worksheet.Cell(row, 2).Value.ToString(), // Fullname
              Status = worksheet.Cell(row, 3).Value.ToString(), // Status
              Role = worksheet.Cell(row, 4).Value.ToString(), // Role
              Avatar = worksheet.Cell(row, 5).Value.ToString(), // Avatar
              Dob = DateTime.TryParse(worksheet.Cell(row, 6).Value.ToString(), out var dob)
                      ? DateOnly.FromDateTime(dob)
                      : (DateOnly?)null, // DOB
              Gender = worksheet.Cell(row, 7).Value.ToString(), // Gender
              Phonenumber = phoneNumber, // PhoneNumber
              Password = BCrypt.Net.BCrypt.HashPassword("defaultPassword123"), // Setup password mặc định
            };

            // Kiểm tra nếu người dùng với số điện thoại đó đã tồn tại
            var existingUser = await _unitOfWork.Repository<User>()
                                                 .GetAllAsync(u => u.Phonenumber == user.Phonenumber);

            if (existingUser.Any())
            {
              // Nếu đã tồn tại, bỏ qua người dùng này
              continue;
            }

            // Nếu fullname không rỗng, thêm người dùng vào danh sách
            if (!string.IsNullOrEmpty(user.Fullname))
            {
              users.Add(user);
            }
          }
        }
      }

      // Lưu các user vào cơ sở dữ liệu
      foreach (var user in users)
      {
        await _unitOfWork.Repository<User>().InsertAsync(user);
      }

      await _unitOfWork.SaveChangesAsync(); // Lưu thay đổi vào CSDL
      return users;
    }


  }
}
