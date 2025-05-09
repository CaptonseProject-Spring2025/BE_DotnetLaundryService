using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ClosedXML.Graphics;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Infrastructure;
using Microsoft.AspNetCore.Http;
using Org.BouncyCastle.Asn1.Ocsp;
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
        private readonly IFileStorageService _fileStorageService;

        public ExcelsService(IUnitOfWork unitOfWork, IUtil util, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _fileStorageService = fileStorageService;
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


        public async Task<ImportLaundryResult> ImportLaundryServicesFromExcel(IFormFile f)
        {
            var rs = new ImportLaundryResult();

            using var stream = new MemoryStream();
            await f.CopyToAsync(stream);
            using var wb = new XLWorkbook(stream);

            var wsExtras = wb.Worksheet("Extras");
            var wsMaster = wb.Worksheet("Master Sheet");

            await _unitOfWork.BeginTransaction();
            try
            {
                // STEP 1 ‑ import EXTRA & EXTRA‑CATEGORY
                var extrasDict = await ImportExtrasAsync(wsExtras, rs);
                // STEP 2 ‑ import CATEGORY + SUBCATEGORY + SERVICE
                await ImportMasterAsync(wsMaster, extrasDict, rs);

                await _unitOfWork.CommitTransaction();
                return rs;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // STEP 1 – Extras
        private async Task<Dictionary<string/*ExtraName*/, Guid/*ExtraId*/>> ImportExtrasAsync(IXLWorksheet ws, ImportLaundryResult rs)
        {
            var extraNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var rows = ws.RangeUsed().RowsUsed().Skip(1); // bỏ header

            foreach (var row in rows)
            {
                var name = row.Cell(1).GetString().Trim();
                var desc = row.Cell(2).GetString().Trim();
                var price = row.Cell(3).GetDouble();
                var catName = row.Cell(5).GetString().Trim();

                if (string.IsNullOrEmpty(name) || price <= 0 || string.IsNullOrEmpty(catName))
                { rs.ErrorRows.Add($"Extras!{row.RowNumber()}"); continue; }

                // Extra‑Category
                var ec = await _unitOfWork.Repository<Extracategory>()
                         .GetAsync(c => c.Name == catName);
                if (ec is null)
                {
                    ec = new Extracategory { Name = catName, Createdat = DateTime.UtcNow };
                    await _unitOfWork.Repository<Extracategory>().InsertAsync(ec);
                    rs.CategoriesInserted++;
                }

                // Check duplicate Extra
                var extra = _unitOfWork.Repository<Extra>().GetAll()
                             .FirstOrDefault(e => e.Name == name && e.Extracategoryid == ec.Extracategoryid);
                if (extra is null)
                {
                    // Image
                    var pic = row.Worksheet.Pictures
                                .FirstOrDefault(p =>
                                    p.TopLeftCell.Address.RowNumber == row.RowNumber() &&
                                    p.TopLeftCell.Address.ColumnNumber == 4);
                    string imgUrl = null;
                    if (pic != null)
                    {
                        using var ms = new MemoryStream();
                        pic.ImageStream.CopyTo(ms);
                        ms.Position = 0;
                        imgUrl = await _fileStorageService.UploadStreamAsync(ms, "extras-test", ".png");
                    }

                    extra = new Extra
                    {
                        Extracategoryid = ec.Extracategoryid,
                        Name = name,
                        Description = desc,
                        Price = (decimal)price,
                        Image = imgUrl,
                        Createdat = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<Extra>().InsertAsync(extra);
                    rs.ExtrasInserted++;
                }

                extraNameToId[name] = extra.Extraid;
            }

            await _unitOfWork.SaveChangesAsync();
            return extraNameToId;
        }

        //STEP 2 – Master Sheet
        private async Task ImportMasterAsync(
            IXLWorksheet ws,
            IReadOnlyDictionary<string, Guid> extraDict,
            ImportLaundryResult rs)
        {
            var rows = ws.RangeUsed().RowsUsed().Skip(1);

            // bộ nhớ tạm để không upload icon/banner trùng
            var uploadedCategoryIcons = new Dictionary<string, string>(); // key: hash|url  value: B2 url
            var uploadedCategoryBanners = new Dictionary<string, string>();

            foreach (var row in rows)
            {
                var svName = row.Cell(1).GetString().Trim();
                var svDesc = row.Cell(3).GetString().Trim();
                var svPrice = row.Cell(4).GetDouble();
                var catName = row.Cell(5).GetString().Trim();
                var subName = row.Cell(8).GetString().Trim();

                if (string.IsNullOrEmpty(svName) || svPrice <= 0
                    || string.IsNullOrEmpty(catName) || string.IsNullOrEmpty(subName))
                { rs.ErrorRows.Add($"Master!{row.RowNumber()}"); continue; }

                /* ---------- CATEGORY ---------- */
                var categoryRepo = _unitOfWork.Repository<Servicecategory>();
                var cat = await categoryRepo.GetAsync(c => c.Name == catName);
                if (cat is null)
                {
                    cat = new Servicecategory { Name = catName };
                    // icon + banner
                    var xlRow = row.WorksheetRow();
                    cat.Icon = await UploadPictureOnce(xlRow, 6, "system-image-test", uploadedCategoryIcons);
                    cat.Banner = await UploadPictureOnce(xlRow, 7, "system-image-test", uploadedCategoryBanners);

                    await categoryRepo.InsertAsync(cat);
                    rs.CategoriesInserted++;
                }

                /* ---------- SUBCATEGORY ---------- */
                var subRepo = _unitOfWork.Repository<Subservice>();
                var sub = subRepo.GetAll()
                         .FirstOrDefault(s => s.Categoryid == cat.Categoryid && s.Name == subName);
                if (sub is null)
                {
                    sub = new Subservice
                    {
                        Categoryid = cat.Categoryid,
                        Name = subName,
                        Description = row.Cell(9).GetString().Trim(),
                        Mincompletetime = row.Cell(10).GetValue<int>(),
                        Createdat = DateTime.UtcNow
                    };
                    await subRepo.InsertAsync(sub);
                    rs.SubCategoriesInserted++;
                }

                /* ---------- SERVICE DETAIL ---------- */
                // Tránh trùng tên service trong Sub
                var svRepo = _unitOfWork.Repository<Servicedetail>();
                var service = svRepo.GetAll()
                             .FirstOrDefault(s => s.Subserviceid == sub.Subserviceid && s.Name == svName);
                if (service is null)
                {
                    var pic = row.Worksheet.Pictures
                                .FirstOrDefault(p =>
                                    p.TopLeftCell.Address.RowNumber == row.RowNumber() &&
                                    p.TopLeftCell.Address.ColumnNumber == 2);
                    string imgUrl = null;
                    if (pic != null)
                    {
                        using var ms = new MemoryStream();
                        pic.ImageStream.CopyTo(ms); ms.Position = 0;
                        imgUrl = await _fileStorageService.UploadStreamAsync(ms, "service-details-test", ".png");
                    }

                    service = new Servicedetail
                    {
                        Subserviceid = sub.Subserviceid,
                        Name = svName,
                        Description = svDesc,
                        Price = (decimal)svPrice,
                        Image = imgUrl,
                        Createdat = DateTime.UtcNow
                    };
                    await svRepo.InsertAsync(service);
                    rs.ServicesInserted++;
                }

                /* ---------- MAPPING EXTRAS ---------- */
                var extraNames = row.Cell(11).GetString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(x => x.Trim())
                                  .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var exName in extraNames)
                {
                    if (extraDict.TryGetValue(exName, out var extraId))
                    {
                        var exists = _unitOfWork.Repository<Serviceextramapping>()
                                     .GetAll()
                                     .Any(m => m.Serviceid == service.Serviceid && m.Extraid == extraId);
                        if (!exists)
                        {
                            await _unitOfWork.Repository<Serviceextramapping>()
                                  .InsertAsync(new Serviceextramapping
                                  {
                                      Serviceid = service.Serviceid,
                                      Extraid = extraId
                                  });
                            rs.ServiceExtraMapped++;
                        }
                    }
                    else
                    {
                        rs.ErrorRows.Add($"ExtraName '{exName}' not found (row {row.RowNumber()})");
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        //upload icon/banner chỉ một lần
        private async Task<string?> UploadPictureOnce(
        IXLRow row, int colIdx,
        string folder,
        IDictionary<string, string> cache)
        {
            var pic = row.Worksheet.Pictures
            .FirstOrDefault(p =>
                p.TopLeftCell.Address.RowNumber == row.RowNumber() &&
                p.TopLeftCell.Address.ColumnNumber == colIdx);
            if (pic == null) return null;

            // hash tạm = chiều rộng+cao+bytes đầu
            string key = $"{pic.OriginalWidth}-{pic.OriginalHeight}";
            if (cache.TryGetValue(key, out var url))
                return url;

            using var ms = new MemoryStream();
            pic.ImageStream.CopyTo(ms); ms.Position = 0;
            url = await _fileStorageService.UploadStreamAsync(ms, folder, ".png");
            cache[key] = url;
            return url;
        }
    }
}
