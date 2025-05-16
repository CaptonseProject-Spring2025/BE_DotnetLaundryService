using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IConfiguration _configuration;
        private readonly IMapboxService _mapboxService;
        private readonly IFileStorageService _fileStorageService;

        public AdminService(IUnitOfWork unitOfWork, IUtil util, IConfiguration configuration, IMapboxService mapboxService, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _configuration = configuration;
            _mapboxService = mapboxService;
            _fileStorageService = fileStorageService;
        }

        public async Task<List<AreaOrdersResponse>> GetConfirmedOrdersByAreaAsync()
        {
            // B1: Lấy danh sách Orders có status = "CONFIRMED"
            var orders = _unitOfWork.Repository<Order>()
                .GetAll()
                .Where(o => o.Currentstatus == "CONFIRMED")
                .Include(o => o.User) // để lấy UserInfo
                .OrderBy(o => o.Createdat) // sắp xếp theo CreatedAt
                .ToList();
            // B2: Đọc config "Areas" từ appsettings
            //     "Areas": {
            //        "Area1": [ "1", "3", "4", "Tân Bình", ... ],
            //        "Area2": [ ... ],
            //        "Area3": [ ... ]
            //     }
            var area1 = _configuration.GetSection("Areas:Area1").Get<string[]>() ?? Array.Empty<string>();
            var area2 = _configuration.GetSection("Areas:Area2").Get<string[]>() ?? Array.Empty<string>();
            var area3 = _configuration.GetSection("Areas:Area3").Get<string[]>() ?? Array.Empty<string>();

            // B3: Lấy toạ độ "AddressDetail" từ appsettings để tính distance
            //    "AddressDetail": {
            //      "Address": "...",
            //      "Latitude": 10.809939,
            //      "Longitude": 106.664737
            //    }
            var addressSection = _configuration.GetSection("AddressDetail");
            decimal refLat = addressSection.GetValue<decimal>("Latitude");
            decimal refLon = addressSection.GetValue<decimal>("Longitude");

            // Tạo dictionary { "Area1": new List<ConfirmedOrderInfo>(), "Area2":..., "Area3":...}
            var areaDict = new Dictionary<string, List<ConfirmedOrderInfo>>()
            {
                { "Area1", new List<ConfirmedOrderInfo>() },
                { "Area2", new List<ConfirmedOrderInfo>() },
                { "Area3", new List<ConfirmedOrderInfo>() },
                // Trường hợp quận không nằm trong 3 area => tuỳ bạn, có thể cho "Outside" hay "Unknown"
            };

            // B4: Duyệt qua Orders -> gọi mapboxService để lấy district
            foreach (var order in orders)
            {
                var pickupLat = order.Pickuplatitude ?? 0;
                var pickupLon = order.Pickuplongitude ?? 0;

                // Gọi mapboxService để lấy tên quận
                var district = await _mapboxService.GetDistrictFromCoordinatesAsync(pickupLat, pickupLon);
                district = district?.Trim() ?? ""; // để phòng null

                // Xác định area
                var areaName = "Unknown";
                // Kiểm tra district có nằm trong "Area1" ?
                if (area1.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area1";
                else if (area2.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area2";
                else if (area3.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area3";

                // Tính distance so với lat/lon trong appsettings
                double distance = _mapboxService.CalculateDistance(pickupLat, pickupLon, refLat, refLon);

                // Tạo object ConfirmedOrderInfo
                var orderInfo = new ConfirmedOrderInfo
                {
                    OrderId = order.Orderid,
                    UserInfo = new UserInfoDto
                    {
                        UserId = order.Userid,
                        FullName = order.User?.Fullname,
                        PhoneNumber = order.User?.Phonenumber
                    },
                    PickupName = order.Pickupname,
                    PickupPhone = order.Pickupphone,
                    Distance = distance,
                    PickupAddressDetail = order.Pickupaddressdetail,
                    PickupDescription = order.Pickupdescription,
                    PickupLatitude = pickupLat,
                    PickupLongitude = pickupLon,
                    PickupTime = order.Pickuptime,
                    CreatedAt = order.Createdat ?? DateTime.UtcNow,
                    TotalPrice = order.Totalprice
                };

                // Thêm vào list tương ứng
                if (!areaDict.ContainsKey(areaName))
                {
                    areaDict[areaName] = new List<ConfirmedOrderInfo>();
                }
                areaDict[areaName].Add(orderInfo);
            }

            // B5: Tạo list kết quả -> group theo Area
            // Mỗi key trong areaDict -> 1 AreaOrdersResponse
            // Sắp xếp trong cùng 1 khu vực theo CreatedAt
            var result = new List<AreaOrdersResponse>();
            foreach (var kv in areaDict)
            {
                // Sắp xếp kv.Value (danh sách ConfirmedOrderInfo) theo CreatedAt
                var sortedOrders = kv.Value
                    .OrderBy(o => o.CreatedAt)
                    .ToList();

                // Bỏ qua group rỗng? Tùy: 
                // - Nếu muốn hiển thị group rỗng => vẫn add
                // - Nếu không => check if (sortedOrders.Any()) ...
                if (sortedOrders.Any())
                {
                    result.Add(new AreaOrdersResponse
                    {
                        Area = kv.Key,
                        Orders = sortedOrders
                    });
                }
            }

            return result;
        }

        public async Task AssignPickupToDriverAsync(HttpContext httpContext, AssignPickupRequest request)
        {
            // 1) Lấy userId admin từ token (người gọi API)
            var adminUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Validate request
            if (request.DriverId == Guid.Empty)
                throw new ArgumentException("DriverId is required.");
            if (request.OrderIds == null || request.OrderIds.Count == 0)
                throw new ArgumentException("OrderIds cannot be empty.");

            // Kiểm tra DriverId có tồn tại và có Role "Driver" không
            var driver = await _unitOfWork.Repository<User>()
                                        .GetAsync(u => u.Userid == request.DriverId && u.Role == "Driver");
            if (driver == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy Driver với ID: {request.DriverId} hoặc người dùng này không phải Driver.");
            }

            // 3) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                // 4) Duyệt từng orderId
                foreach (var orderId in request.OrderIds)
                {
                    // a) Tìm Order => validate
                    var order = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .FirstOrDefault(o => o.Orderid == orderId);

                    if (order == null)
                    {
                        throw new KeyNotFoundException($"OrderId {orderId} not found.");
                    }

                    // Chỉ assign pickup nếu order đang CONFIRMED
                    if (order.Currentstatus != OrderStatusEnum.CONFIRMED.ToString())
                    {
                        throw new ApplicationException(
                            $"Order {orderId} is not in CONFIRMED status. Current: {order.Currentstatus}"
                        );
                    }

                    // b) Tạo record Orderassignmenthistory
                    var assignment = new Orderassignmenthistory
                    {
                        Orderid = orderId,
                        Assignedto = request.DriverId,
                        Assignedat = DateTime.UtcNow,
                        Status = AssignStatusEnum.ASSIGNED_PICKUP.ToString(), // từ enum AssignStatusEnum
                    };
                    await _unitOfWork.Repository<Orderassignmenthistory>().InsertAsync(assignment, saveChanges: false);

                    // c) Tạo record Orderstatushistory
                    var statusHistory = new Orderstatushistory
                    {
                        Orderid = orderId,
                        Status = OrderStatusEnum.SCHEDULED_PICKUP.ToString(),
                        Statusdescription = "Đã lên lịch lấy hàng",
                        Updatedby = adminUserId,
                        Createdat = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(statusHistory, saveChanges: false);

                    // d) Cập nhật Order -> Currentstatus = "SCHEDULED_PICKUP"
                    order.Currentstatus = OrderStatusEnum.SCHEDULED_PICKUP.ToString();
                    await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);
                }

                // 5) Lưu và commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<Guid> GetCustomerIdByOrderAsync(string orderId)
        {
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Orderid == orderId);

            if (order == null)
                throw new KeyNotFoundException("Không tìm thấy đơn hàng.");

            return order.Userid; //  Trường chứa customer ID
        }

        public async Task<List<AreaOrdersResponse>> GetQualityCheckedOrdersByAreaAsync()
        {
            // B1: Lấy các Orders => Currentstatus = "QUALITY_CHECKED"
            var statusQC = OrderStatusEnum.QUALITY_CHECKED.ToString();
            var orders = _unitOfWork.Repository<Order>()
                .GetAll()
                .Where(o => o.Currentstatus == statusQC)
                .Include(o => o.User) // lấy user
                .OrderBy(o => o.Createdat) // tạm sắp xếp theo CreatedAt (để lát nữa nhóm theo khu)
                .ToList();

            // B2: Lấy config "Areas" từ settings
            var area1 = _configuration.GetSection("Areas:Area1").Get<string[]>() ?? Array.Empty<string>();
            var area2 = _configuration.GetSection("Areas:Area2").Get<string[]>() ?? Array.Empty<string>();
            var area3 = _configuration.GetSection("Areas:Area3").Get<string[]>() ?? Array.Empty<string>();

            // B3: Lấy config "AddressDetail" (để tính distance)
            var addressSection = _configuration.GetSection("AddressDetail");
            decimal refLat = addressSection.GetValue<decimal>("Latitude");
            decimal refLon = addressSection.GetValue<decimal>("Longitude");

            // Tạo dictionary group area
            var areaDict = new Dictionary<string, List<ConfirmedOrderInfo>>()
            {
                { "Area1", new List<ConfirmedOrderInfo>() },
                { "Area2", new List<ConfirmedOrderInfo>() },
                { "Area3", new List<ConfirmedOrderInfo>() }
                // Hoặc để sau if "Unknown"
            };

            // B4: Với mỗi order => gọi mapboxService để lấy district => xác định area
            foreach (var order in orders)
            {
                var pickupLat = order.Pickuplatitude ?? 0;
                var pickupLon = order.Pickuplongitude ?? 0;

                // 4a) Gọi mapboxService => lấy district
                var district = await _mapboxService.GetDistrictFromCoordinatesAsync(pickupLat, pickupLon);
                district = district?.Trim() ?? "";

                // 4b) Xác định area
                var areaName = "Unknown";
                if (area1.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area1";
                else if (area2.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area2";
                else if (area3.Contains(district, StringComparer.OrdinalIgnoreCase))
                    areaName = "Area3";

                // 4c) Tính distance
                double distance = _mapboxService.CalculateDistance(pickupLat, pickupLon, refLat, refLon);

                // 4d) Chuyển CreatedAt, PickupTime sang giờ VN (nếu cần)
                var createdAtVn = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow);
                DateTime? pickupTimeVn = null;
                if (order.Pickuptime.HasValue)
                {
                    pickupTimeVn = _util.ConvertToVnTime(order.Pickuptime.Value);
                }

                // 4e) Tạo object ConfirmedOrderInfo
                var orderInfo = new ConfirmedOrderInfo
                {
                    OrderId = order.Orderid,
                    UserInfo = new UserInfoDto
                    {
                        UserId = order.Userid,
                        FullName = order.User?.Fullname,
                        PhoneNumber = order.User?.Phonenumber
                    },
                    PickupName = order.Pickupname,
                    PickupPhone = order.Pickupphone,
                    Distance = distance,
                    PickupAddressDetail = order.Pickupaddressdetail,
                    PickupDescription = order.Pickupdescription,
                    PickupLatitude = pickupLat,
                    PickupLongitude = pickupLon,
                    PickupTime = pickupTimeVn,
                    // Sử dụng thời gian đã convert về VN
                    CreatedAt = createdAtVn,
                    TotalPrice = order.Totalprice
                };

                // 4f) Thêm vào dictionary
                if (!areaDict.ContainsKey(areaName))
                {
                    areaDict[areaName] = new List<ConfirmedOrderInfo>();
                }
                areaDict[areaName].Add(orderInfo);
            }

            // B5: Tạo danh sách kết quả
            var result = new List<AreaOrdersResponse>();
            foreach (var kv in areaDict)
            {
                var listOrders = kv.Value
                    .OrderBy(o => o.CreatedAt) // sắp xếp theo CreatedAt (trong cùng khu vực)
                    .ToList();

                // Nếu muốn bỏ qua area rỗng => check if (listOrders.Any())
                if (listOrders.Any())
                {
                    result.Add(new AreaOrdersResponse
                    {
                        Area = kv.Key,
                        Orders = listOrders
                    });
                }
            }

            return result;
        }

        public async Task AssignDeliveryToDriverAsync(HttpContext httpContext, AssignPickupRequest request)
        {
            // 1) Lấy AdminUserId (người gọi API) từ JWT
            var adminUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Validate request
            if (request.DriverId == Guid.Empty)
                throw new ArgumentException("DriverId is required.");

            if (request.OrderIds == null || request.OrderIds.Count == 0)
                throw new ArgumentException("OrderIds cannot be empty.");

            // Kiểm tra DriverId có tồn tại & có Role = "Driver" hay không
            var driver = await _unitOfWork.Repository<User>()
                                          .GetAsync(u => u.Userid == request.DriverId && u.Role == "Driver");
            if (driver == null)
            {
                throw new KeyNotFoundException(
                    $"Không tìm thấy Driver với ID: {request.DriverId} hoặc người này không phải Driver."
                );
            }

            // 3) Bắt đầu Transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                // 4) Lặp qua từng OrderId
                foreach (var orderId in request.OrderIds)
                {
                    // a) Tìm Order -> kiểm tra
                    var order = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .FirstOrDefault(o => o.Orderid == orderId);

                    if (order == null)
                        throw new KeyNotFoundException($"OrderId '{orderId}' không tồn tại.");

                    // (Tuỳ logic) Kiểm tra order có đang "QUALITY_CHECKED" hay không
                    // để đảm bảo logic (chỉ những đơn giặt xong, đã QA-check xong mới giao)
                    if (order.Currentstatus != OrderStatusEnum.QUALITY_CHECKED.ToString())
                    {
                        throw new ApplicationException(
                            $"Order {orderId} chưa sẵn sàng để giao. Trạng thái hiện tại: {order.Currentstatus}"
                        );
                    }

                    // b) Tạo record Orderassignmenthistory
                    var assignment = new Orderassignmenthistory
                    {
                        Orderid = orderId,
                        Assignedto = request.DriverId,
                        Assignedat = DateTime.UtcNow,
                        // Giao cho driver => status = "ASSIGNED_DELIVERY"
                        Status = AssignStatusEnum.ASSIGNED_DELIVERY.ToString()
                    };
                    await _unitOfWork.Repository<Orderassignmenthistory>()
                                     .InsertAsync(assignment, saveChanges: false);

                    // c) Tạo record Orderstatushistory
                    var statusHistory = new Orderstatushistory
                    {
                        Orderid = orderId,
                        Status = OrderStatusEnum.SCHEDULED_DELIVERY.ToString(),
                        Statusdescription = "Đã lên lịch giao hàng. Bạn sẽ sớm nhận được đơn hàng.",
                        Updatedby = adminUserId,
                        Createdat = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<Orderstatushistory>()
                                     .InsertAsync(statusHistory, saveChanges: false);

                    // d) Cập nhật Order => Currentstatus = "SCHEDULED_DELIVERY"
                    order.Currentstatus = OrderStatusEnum.SCHEDULED_DELIVERY.ToString();
                    await _unitOfWork.Repository<Order>()
                                     .UpdateAsync(order, saveChanges: false);
                }

                // 5) SaveChanges + Commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                // Rollback nếu có lỗi
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task DeleteOrderAsync(string orderId)
        {
            // Bắt đầu Transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // Tìm Order => nếu ko có => throw
                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Orderid == orderId);

                if (order == null)
                    throw new KeyNotFoundException($"Không tìm thấy OrderId = {orderId}");

                // Lấy hết OrderStatusHistory => sau đó xoá OrderPhotos
                var orderStatusHistories = _unitOfWork.Repository<Orderstatushistory>()
                    .GetAll()
                    .Include(x => x.Orderphotos)  // cần Include để load list OrderPhoto
                    .Where(x => x.Orderid == orderId)
                    .ToList();

                // 1) Xoá ảnh (OrderPhotos) trên Backblaze + DB
                foreach (var history in orderStatusHistories)
                {
                    var photos = history.Orderphotos.ToList();
                    foreach (var photo in photos)
                    {
                        // Xoá file B2
                        await _fileStorageService.DeleteFileAsync(photo.Photourl);

                        // Xoá record OrderPhoto
                        await _unitOfWork.Repository<Orderphoto>()
                                         .DeleteAsync(photo, saveChanges: false);
                    }
                }

                // 2) Xoá OrderStatusHistory
                foreach (var history in orderStatusHistories)
                {
                    await _unitOfWork.Repository<Orderstatushistory>()
                                     .DeleteAsync(history, saveChanges: false);
                }

                // Xoá OrderExtras => reliant on OrderItems
                //    => Lấy các OrderItem => Lấy OrderExtra
                var orderItemIds = _unitOfWork.Repository<Orderitem>()
                    .GetAll()
                    .Where(oi => oi.Orderid == orderId)
                    .Select(oi => oi.Orderitemid)
                    .ToList();

                // 3) Lấy tất cả OrderExtras => xóa
                var orderExtras = _unitOfWork.Repository<Orderextra>()
                    .GetAll()
                    .Where(oe => orderItemIds.Contains(oe.Orderitemid))
                    .ToList();
                foreach (var oe in orderExtras)
                {
                    await _unitOfWork.Repository<Orderextra>()
                                     .DeleteAsync(oe, saveChanges: false);
                }

                // 4) Xoá OrderItems
                var orderItems = _unitOfWork.Repository<Orderitem>()
                    .GetAll()
                    .Where(oi => oi.Orderid == orderId)
                    .ToList();
                foreach (var item in orderItems)
                {
                    await _unitOfWork.Repository<Orderitem>()
                                     .DeleteAsync(item, saveChanges: false);
                }

                // 5) Xoá OrderAssignmentHistory
                var assignmentHistories = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .Where(ah => ah.Orderid == orderId)
                    .ToList();
                foreach (var ah in assignmentHistories)
                {
                    await _unitOfWork.Repository<Orderassignmenthistory>()
                                     .DeleteAsync(ah, saveChanges: false);
                }

                // 5) Xoá Complaints
                var complaints = _unitOfWork.Repository<Complaint>()
                    .GetAll()
                    .Where(com => com.Orderid == orderId)
                    .ToList();
                foreach (var com in complaints)
                {
                    await _unitOfWork.Repository<Complaint>()
                                     .DeleteAsync(com, saveChanges: false);
                }

                // 6) Xoá Payments
                var payments = _unitOfWork.Repository<Payment>()
                    .GetAll()
                    .Where(p => p.Orderid == orderId)
                    .ToList();
                foreach (var pay in payments)
                {
                    await _unitOfWork.Repository<Payment>()
                                     .DeleteAsync(pay, saveChanges: false);
                }

                // 7) Xoá DriverLocationHistory
                var driverLoc = _unitOfWork.Repository<Driverlocationhistory>()
                    .GetAll()
                    .Where(d => d.Orderid == orderId)
                    .ToList();
                foreach (var dlh in driverLoc)
                {
                    await _unitOfWork.Repository<Driverlocationhistory>()
                                     .DeleteAsync(dlh, saveChanges: false);
                }

                // 8) Xoá Ratings
                var ratings = _unitOfWork.Repository<Rating>()
                    .GetAll()
                    .Where(r => r.Orderid == orderId)
                    .ToList();
                foreach (var r in ratings)
                {
                    await _unitOfWork.Repository<Rating>()
                                     .DeleteAsync(r, saveChanges: false);
                }

                // 9) Xoá OrderDiscounts
                var orderDiscounts = _unitOfWork.Repository<Orderdiscount>()
                    .GetAll()
                    .Where(od => od.Orderid == orderId)
                    .ToList();
                foreach (var od in orderDiscounts)
                {
                    await _unitOfWork.Repository<Orderdiscount>()
                                     .DeleteAsync(od, saveChanges: false);
                }

                // 10) Xoá Order
                await _unitOfWork.Repository<Order>().DeleteAsync(order, saveChanges: false);

                // Save + commit transaction
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                // rollback nếu có lỗi
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task DeleteOrdersAsync(List<string> orderIds)
        {
            if (orderIds == null || orderIds.Count == 0)
            {
                throw new ArgumentException("Danh sách OrderIds trống.");
            }

            // Mở transaction duy nhất cho toàn bộ quá trình xóa
            await _unitOfWork.BeginTransaction();
            try
            {
                foreach (var orderId in orderIds)
                {
                    // Tìm Order => nếu không có => throw
                    var order = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .FirstOrDefault(o => o.Orderid == orderId);

                    if (order == null)
                        throw new KeyNotFoundException($"Không tìm thấy OrderId = {orderId}");

                    // Lấy hết OrderStatusHistory => Include OrderPhotos
                    var orderStatusHistories = _unitOfWork.Repository<Orderstatushistory>()
                        .GetAll()
                        .Include(x => x.Orderphotos)
                        .Where(x => x.Orderid == orderId)
                        .ToList();

                    // 1) Xoá ảnh (OrderPhotos) trên Backblaze + DB
                    foreach (var history in orderStatusHistories)
                    {
                        var photos = history.Orderphotos.ToList();
                        foreach (var photo in photos)
                        {
                            // Xoá file B2
                            await _fileStorageService.DeleteFileAsync(photo.Photourl);

                            // Xoá record OrderPhoto
                            await _unitOfWork.Repository<Orderphoto>()
                                             .DeleteAsync(photo, saveChanges: false);
                        }
                    }

                    // 2) Xoá OrderStatusHistory
                    foreach (var history in orderStatusHistories)
                    {
                        await _unitOfWork.Repository<Orderstatushistory>()
                                         .DeleteAsync(history, saveChanges: false);
                    }

                    // Lấy tất cả OrderItemIds
                    var orderItemIds = _unitOfWork.Repository<Orderitem>()
                        .GetAll()
                        .Where(oi => oi.Orderid == orderId)
                        .Select(oi => oi.Orderitemid)
                        .ToList();

                    // 3) Xoá OrderExtras
                    var orderExtras = _unitOfWork.Repository<Orderextra>()
                        .GetAll()
                        .Where(oe => orderItemIds.Contains(oe.Orderitemid))
                        .ToList();
                    foreach (var oe in orderExtras)
                    {
                        await _unitOfWork.Repository<Orderextra>()
                                         .DeleteAsync(oe, saveChanges: false);
                    }

                    // 4) Xoá OrderItems
                    var orderItems = _unitOfWork.Repository<Orderitem>()
                        .GetAll()
                        .Where(oi => oi.Orderid == orderId)
                        .ToList();
                    foreach (var item in orderItems)
                    {
                        await _unitOfWork.Repository<Orderitem>()
                                         .DeleteAsync(item, saveChanges: false);
                    }

                    // 5) Xoá OrderAssignmentHistory
                    var assignmentHistories = _unitOfWork.Repository<Orderassignmenthistory>()
                        .GetAll()
                        .Where(ah => ah.Orderid == orderId)
                        .ToList();
                    foreach (var ah in assignmentHistories)
                    {
                        await _unitOfWork.Repository<Orderassignmenthistory>()
                                         .DeleteAsync(ah, saveChanges: false);
                    }

                    // 6) Xoá Payments
                    var payments = _unitOfWork.Repository<Payment>()
                        .GetAll()
                        .Where(p => p.Orderid == orderId)
                        .ToList();
                    foreach (var pay in payments)
                    {
                        await _unitOfWork.Repository<Payment>()
                                         .DeleteAsync(pay, saveChanges: false);
                    }

                    // 7) Xoá DriverLocationHistory
                    var driverLoc = _unitOfWork.Repository<Driverlocationhistory>()
                        .GetAll()
                        .Where(d => d.Orderid == orderId)
                        .ToList();
                    foreach (var dlh in driverLoc)
                    {
                        await _unitOfWork.Repository<Driverlocationhistory>()
                                         .DeleteAsync(dlh, saveChanges: false);
                    }

                    // 8) Xoá Ratings
                    var ratings = _unitOfWork.Repository<Rating>()
                        .GetAll()
                        .Where(r => r.Orderid == orderId)
                        .ToList();
                    foreach (var r in ratings)
                    {
                        await _unitOfWork.Repository<Rating>()
                                         .DeleteAsync(r, saveChanges: false);
                    }

                    // 9) Xoá OrderDiscounts
                    var orderDiscounts = _unitOfWork.Repository<Orderdiscount>()
                        .GetAll()
                        .Where(od => od.Orderid == orderId)
                        .ToList();
                    foreach (var od in orderDiscounts)
                    {
                        await _unitOfWork.Repository<Orderdiscount>()
                                         .DeleteAsync(od, saveChanges: false);
                    }

                    // 10) Cuối cùng, xóa Order
                    await _unitOfWork.Repository<Order>()
                                     .DeleteAsync(order, saveChanges: false);
                }

                // Lưu + commit transaction
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                // rollback nếu có lỗi
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
