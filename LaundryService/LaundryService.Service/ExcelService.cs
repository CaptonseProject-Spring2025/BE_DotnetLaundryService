using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ClosedXML.Graphics;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Infrastructure;
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
        private readonly IUtil _util;
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly Dictionary<string, byte[]> _categoryIconCache = new();

        public ExcelsService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }

        private static void StyleHeader(IXLRow headerRow)
        {
            headerRow.Style.Fill.BackgroundColor = XLColor.CornflowerBlue;
            headerRow.Style.Font.FontColor = XLColor.White;
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static async Task EmbedImageInCellAsync(IXLCell cell, string url, HttpClient client)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                // Xóa nội dung cũ nếu có và không có URL hợp lệ
                cell.Clear(XLClearOptions.Contents);
                // Bạn có thể đặt một giá trị placeholder nếu muốn, ví dụ: cell.Value = "No Image";
                return;
            }

            try
            {
                byte[] imageBytes = await client.GetByteArrayAsync(url);
                using (var ms = new MemoryStream(imageBytes))
                {
                    // Xóa nội dung text (ví dụ URL) khỏi ô trước khi chèn ảnh
                    cell.Clear(XLClearOptions.Contents);

                    var picture = cell.Worksheet.AddPicture(ms).MoveTo(cell); // Đặt góc trên trái của ảnh vào góc trên trái của ô

                    // Đặt chiều cao dòng cố định
                    cell.WorksheetRow().Height = 90;

                    // Scale ảnh để vừa với chiều cao dòng (ví dụ: 85pt, để lại chút padding 2.5pt trên và dưới)
                    const double desiredImageHeightPt = 85.0;
                    if (picture.OriginalHeight > 0 && picture.OriginalWidth > 0) // Tránh chia cho 0
                    {
                        double scaleFactor = desiredImageHeightPt / picture.OriginalHeight;
                        picture.Scale(scaleFactor); // Scale giữ tỷ lệ

                        // Căn giữa ảnh theo chiều dọc trong ô (sau khi đã scale và set row height)
                        double cellHeightPt = cell.WorksheetRow().Height; // Là 90
                        if (picture.Height < cellHeightPt)
                        {
                            //picture.OffsetY = (int)((cellHeightPt - picture.Height) / 2);
                        }
                        // Căn giữa theo chiều ngang phức tạp hơn nếu không có kích thước cột chính xác bằng point.
                        // Hiện tại ảnh sẽ căn trái trong ô.
                    }
                    // Đảm bảo ô được căn giữa (cho trường hợp có text lỗi hoặc placeholder)
                    cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                    cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }
            }
            catch (HttpRequestException)
            {
                cell.Clear(XLClearOptions.Contents);
                cell.Value = "Tải ảnh lỗi";
                cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            catch (Exception) // Các lỗi khác (ví dụ: ảnh hỏng, stream lỗi)
            {
                cell.Clear(XLClearOptions.Contents);
                cell.Value = "Ảnh lỗi";
                cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
        }

        private async Task EmbedCategoryIconAsync(IXLCell cell, string url)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                cell.Clear(XLClearOptions.Contents);
                return;
            }

            try
            {
                if (!_categoryIconCache.TryGetValue(url, out var bytes))
                {
                    // Lần đầu gặp URL này → tải và lưu cache
                    bytes = await httpClient.GetByteArrayAsync(url);
                    _categoryIconCache[url] = bytes;
                }

                using var ms = new MemoryStream(bytes);
                var pic = cell.Worksheet.AddPicture(ms)
                                        .MoveTo(cell)
                                        .Scale(0.2);

                cell.WorksheetRow().Height =
                    Math.Max(cell.WorksheetRow().Height, pic.Height * 0.75);

                cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            }
            catch
            {
                cell.Clear(XLClearOptions.Contents);
                cell.Value = "IconErr";
                cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
        }

        private static void SetImageFormula(IXLCell cell, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // IMAGE("url") – KHÔNG có dấu =
            cell.FormulaA1 = $"IMAGE(\"{url}\")";

            // tuỳ thích: đặt kích thước ô cao hơn cho dễ nhìn
            cell.WorksheetRow().Height = 90;       // ví dụ 90 px
            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }

        public async Task<byte[]> ExportUsersToExcel()
        {
            LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");

            // Lấy danh sách người dùng từ CSDL
            var users = await _unitOfWork.Repository<User>().GetAllAsync();

            // Tạo một Workbook mới
            using var workbook = new XLWorkbook();
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
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
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

        public async Task<byte[]> ExportLaundryServicesToExcel()
        {
            // 1) Lấy dữ liệu cần thiết
            var serviceDetails = _unitOfWork.Repository<Servicedetail>()
                .GetAll()
                .ToList();                                 // dùng navigation property

            var serviceCategories = await _unitOfWork.Repository<Servicecategory>().GetAllAsync();
            var subServices = await _unitOfWork.Repository<Subservice>().GetAllAsync();
            var extras = await _unitOfWork.Repository<Extra>().GetAllAsync();
            var extraCategories = await _unitOfWork.Repository<Extracategory>().GetAllAsync();
            var mappings = _unitOfWork.Repository<Serviceextramapping>().GetAll().ToList();

            // 2) Tạo workbook
            using var workbook = new XLWorkbook();

            /*****************  SHEET 0 – Master Sheet  *****************/
            var wsMaster = workbook.Worksheets.Add("Master Sheet");

            // Header
            string[] masterHeaders = {
                "Service Name", "Service Image", "Service Description", "Service Price",
                "Category Name", "Category Icon", "Category Banner",
                "SubCategory Name", "SubCategory Description", "SubCategory Mincompletetime",
                "Extras"
            };
            for (int i = 0; i < masterHeaders.Length; i++)
                wsMaster.Cell(1, i + 1).SetValue(masterHeaders[i]);

            StyleHeader(wsMaster.Row(1));

            // Body
            int mRow = 2;
            foreach (var sd in serviceDetails)
            {
                var sub = subServices.FirstOrDefault(s => s.Subserviceid == sd.Subserviceid);
                var cate = serviceCategories.FirstOrDefault(c => c.Categoryid == sub?.Categoryid);

                /*--- Lấy danh sách Extra NAMES gắn với ServiceDetail ---*/
                var extraNames = mappings
                    .Where(m => m.Serviceid == sd.Serviceid)
                    .Join(extras, m => m.Extraid, e => e.Extraid, (m, e) => e.Name)
                    .ToList();
                var extrasNewLine = string.Join("\n", extraNames);    // mỗi extra 1 dòng

                // Ghi dữ liệu
                wsMaster.Cell(mRow, 1).SetValue(sd.Name);
                await EmbedImageInCellAsync(wsMaster.Cell(mRow, 2), sd.Image, httpClient);
                wsMaster.Cell(mRow, 3).SetValue(sd.Description);
                wsMaster.Cell(mRow, 4).SetValue(sd.Price);
                wsMaster.Cell(mRow, 5).SetValue(cate?.Name);
                await EmbedCategoryIconAsync(wsMaster.Cell(mRow, 6), cate?.Icon);
                await EmbedImageInCellAsync(wsMaster.Cell(mRow, 7), cate?.Banner, httpClient);
                wsMaster.Cell(mRow, 8).SetValue(sub?.Name);
                wsMaster.Cell(mRow, 9).SetValue(sub?.Description);
                wsMaster.Cell(mRow, 10).SetValue(sub?.Mincompletetime);
                wsMaster.Cell(mRow, 11).SetValue(extrasNewLine);
                wsMaster.Cell(mRow, 11).Style.Alignment.WrapText = true;   // xuống dòng hiển thị
                mRow++;
            }

            wsMaster.Columns().AdjustToContents();

            // style
            var rngMaster = wsMaster.Range(1, 1, mRow - 1, masterHeaders.Length);
            var tblMaster = rngMaster.CreateTable();
            tblMaster.Theme = XLTableTheme.TableStyleLight9;
            tblMaster.ShowRowStripes = true;

            /*************** SHEET 2 – Extras ****************/
            var wsExtra = workbook.Worksheets.Add("Extras");
            wsExtra.Cell(1, 1).InsertTable(extras.Select(e =>
            {
                var ec = extraCategories.FirstOrDefault(x => x.Extracategoryid == e.Extracategoryid);
                return new
                {
                    e.Name,
                    e.Description,
                    e.Price,
                    e.Image,
                    CreatedAt = _util.ConvertToVnTime(e.Createdat ?? DateTime.UtcNow),
                    ExtraCategoryName = ec?.Name,
                    ExtraCategoryCreate = _util.ConvertToVnTime(ec?.Createdat ?? DateTime.UtcNow)
                };
            }));
            int exLastRow = wsExtra.LastRowUsed().RowNumber();
            for (int r = 2; r <= exLastRow; r++)
            {
                SetImageFormula(wsExtra.Cell(r, 4), wsExtra.Cell(r, 4).GetString()); // Image (col 4)
            }

            wsExtra.Columns().AdjustToContents();

            // 3) Trả về mảng bytes
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
