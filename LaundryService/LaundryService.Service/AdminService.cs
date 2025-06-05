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
        private readonly IMapboxService _mapboxService;
        private readonly IFileStorageService _fileStorageService;

        public AdminService(IUnitOfWork unitOfWork, IUtil util, IMapboxService mapboxService, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _mapboxService = mapboxService;
            _fileStorageService = fileStorageService;
        }

        public async Task<List<AreaOrdersResponse>> GetConfirmedOrdersByAreaAsync()
        {
            /* ---------- 1. Đọc Order theo 4 trạng thái ---------- */
            var wantedStatuses = new[]
            {
                OrderStatusEnum.CONFIRMED.ToString(),
                OrderStatusEnum.SCHEDULED_PICKUP.ToString(),
                OrderStatusEnum.PICKINGUP.ToString(),
                OrderStatusEnum.PICKUPFAILED.ToString()
            };

            var orders = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .Where(o => wantedStatuses.Contains(o.Currentstatus!))
                        .Include(o => o.User)
                        .Include(o => o.Orderassignmenthistories)   // dùng bước 2
                        .Include(o => o.Orderstatushistories)       // dùng bước 3
                        .ToList();

            /* ---------- 2. Lọc SCHEDULED_PICKUP | PICKINGUP ---------- */
            orders = orders.Where(o =>
            {
                var st = (o.Currentstatus ?? string.Empty)
                            .Trim()
                            .ToUpperInvariant();

                if (st != OrderStatusEnum.SCHEDULED_PICKUP.ToString() &&
                    st != OrderStatusEnum.PICKINGUP.ToString())
                    return true;   // CONFIRMED hoặc PICKUPFAILED → giữ

                // lấy assignment gần nhất
                var lastAss = o.Orderassignmenthistories
                               .OrderByDescending(a => a.Assignedat)
                               .FirstOrDefault();

                return lastAss != null &&
                       lastAss.Completedat.HasValue &&
                       string.Equals(lastAss.Status?.Trim(),
                                     AssignStatusEnum.PICKUP_FAILED.ToString(),
                                     StringComparison.OrdinalIgnoreCase);
            }).ToList();

            // ---------- Lấy danh sách Area (Driver) ----------
            var driverAreas = _unitOfWork.Repository<Area>()
                                         .GetAll()
                                         .Where(a => a.Areatype.ToUpper() == "DRIVER")
                                         .ToList();

            //Build tra cứu: district -> areaName
            //dùng ToDictionary vì district là duy nhất trong mỗi Area
            var districtToArea = driverAreas
                                 .Where(a => a.Districts != null) // bỏ khu vực chưa khai Districts
                                 .SelectMany(a => a.Districts!
                                                   .Select(d => new { District = d, AreaName = a.Name }))
                                 .ToDictionary(
                                     x => x.District, // key   = tên quận
                                     x => x.AreaName, // value = tên khu vực
                                     StringComparer.OrdinalIgnoreCase); // so sánh không phân biệt hoa thường

            //------- Lấy lat/lon trung tâm ----------
            var branchAddress = _unitOfWork.Repository<Branchaddress>().GetAll().FirstOrDefault();
            decimal refLat = branchAddress.Latitude ?? 0;
            decimal refLon = branchAddress.Longitude ?? 0;

            // ---------- Gom nhóm & tính UserDeclineCount ----------
            // Dictionary<AreaName, List<ConfirmedOrderInfo>>
            var areaDict = new Dictionary<string, List<ConfirmedOrderInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (var order in orders)
            {
                // đếm số lần PICKUPFAILED
                int declineCnt = order.Orderstatushistories.Count(h =>
                     string.Equals(h.Status?.Trim(),
                                   OrderStatusEnum.PICKUPFAILED.ToString(),
                                   StringComparison.OrdinalIgnoreCase));

                var lat = order.Pickuplatitude ?? 0;
                var lon = order.Pickuplongitude ?? 0;

                // Mapbox => tên quận
                var district = await _mapboxService.GetDistrictFromCoordinatesAsync(lat, lon);
                district = district?.Trim() ?? string.Empty;

                // Tìm Area tương ứng
                var areaName = districtToArea.TryGetValue(district, out var a) ? a : "Unknown";

                // Tính khoảng cách
                double distance = _mapboxService.CalculateDistance(lat, lon, refLat, refLon);

                //Build ConfirmedOrderInfo
                var info = new ConfirmedOrderInfo
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
                    PickupLatitude = lat,
                    PickupLongitude = lon,
                    PickupTime = order.Pickuptime,
                    CreatedAt = order.Createdat ?? DateTime.UtcNow,
                    TotalPrice = order.Totalprice,
                    UserDeclineCount = declineCnt
                };

                if (!areaDict.ContainsKey(areaName))
                    areaDict[areaName] = new List<ConfirmedOrderInfo>();

                areaDict[areaName].Add(info);
            }

            //---------- 5. Trả kết quả ----------
            var result = areaDict
                .Where(kv => kv.Value.Any()) // bỏ nhóm rỗng
                .Select(kv => new AreaOrdersResponse
                {
                    Area = kv.Key,
                    Orders = kv.Value.OrderBy(i => i.PickupTime ?? DateTime.MaxValue).ToList()
                })
                .OrderBy(r => r.Area) // sắp Area theo tên
                .ToList();

            return result;
        }

        public async Task AssignPickupToDriverAsync(HttpContext httpContext, AssignPickupRequest request)
        {
            // Lấy userId admin từ token
            var adminUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            // Validate request
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

                    // Tạo row Orderassignmenthistory
                    var assignment = new Orderassignmenthistory
                    {
                        Orderid = orderId,
                        Assignedto = request.DriverId,
                        Assignedat = DateTime.UtcNow,
                        Status = AssignStatusEnum.ASSIGNED_PICKUP.ToString(), // từ enum AssignStatusEnum
                    };
                    await _unitOfWork.Repository<Orderassignmenthistory>().InsertAsync(assignment, saveChanges: false);

                    // Nếu order chưa có status history là "SCHEDULED_PICKUP" hoặc "PICKINGUP"
                    if (order.Currentstatus != OrderStatusEnum.SCHEDULED_PICKUP.ToString() &&
                        order.Currentstatus != OrderStatusEnum.PICKINGUP.ToString())
                    {
                        // Tạo row Orderstatushistory
                        var statusHistory = new Orderstatushistory
                        {
                            Orderid = orderId,
                            Status = OrderStatusEnum.SCHEDULED_PICKUP.ToString(),
                            Statusdescription = "Đã lên lịch lấy hàng",
                            Updatedby = adminUserId,
                            Createdat = DateTime.UtcNow
                        };
                        await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(statusHistory, saveChanges: false);

                        // cập nhật Order.Currentstatus = "SCHEDULED_PICKUP"
                        order.Currentstatus = OrderStatusEnum.SCHEDULED_PICKUP.ToString();
                        await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);
                    }
                }

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

            return order.Userid;
        }

        public async Task<List<AreaOrdersResponse>> GetQualityCheckedOrdersByAreaAsync()
        {
            /* ---------- Đọc Order theo 4 trạng thái ---------- */
            var wantedStatuses = new[]
            {
                OrderStatusEnum.QUALITY_CHECKED.ToString(),
                OrderStatusEnum.DELIVERYFAILED.ToString(),
                OrderStatusEnum.SCHEDULED_DELIVERY.ToString(),
                OrderStatusEnum.DELIVERING.ToString()
            };
            var orders = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .Where(o => wantedStatuses.Contains(o.Currentstatus!))
                        .Include(o => o.User)
                        .Include(o => o.Orderassignmenthistories)   // dùng bước 2
                        .Include(o => o.Orderstatushistories)       // dùng bước 3
                        .ToList();

            /* ---------- Lọc SCHEDULED_PICKUP | PICKINGUP ---------- */
            orders = orders.Where(o =>
            {
                var st = (o.Currentstatus ?? string.Empty)
                            .Trim()
                            .ToUpperInvariant();

                if (st != OrderStatusEnum.SCHEDULED_DELIVERY.ToString() &&
                    st != OrderStatusEnum.DELIVERING.ToString())
                    return true;   // QUALITY_CHECKED hoặc DELIVERYFAILED -> giữ

                // lấy assignment gần nhất
                var lastAss = o.Orderassignmenthistories
                               .OrderByDescending(a => a.Assignedat)
                               .FirstOrDefault();

                return lastAss != null &&
                       lastAss.Completedat.HasValue &&
                       string.Equals(lastAss.Status?.Trim(),
                                     AssignStatusEnum.DELIVERY_FAILED.ToString(),
                                     StringComparison.OrdinalIgnoreCase);
            }).ToList();

            // ---------- Lấy danh sách Area (Driver) từ DB ----------
            var driverAreas = _unitOfWork.Repository<Area>()
                                         .GetAll()
                                         .Where(a => a.Areatype.ToUpper() == "DRIVER")
                                         .ToList();

            // Build tra cứu: tên quận -> tên khu vực
            var districtToArea = driverAreas
                .Where(a => a.Districts != null && a.Districts.Count > 0)
                .SelectMany(a => a.Districts!
                                  .Select(d => new { District = d, AreaName = a.Name }))
                .ToDictionary(
                    x => x.District,
                    x => x.AreaName,
                    StringComparer.OrdinalIgnoreCase);

            // ---------- Tọa độ chi nhánh để tính distance ----------
            var branchAddress = _unitOfWork.Repository<Branchaddress>().GetAll().FirstOrDefault();
            decimal refLat = branchAddress.Latitude ?? 0;
            decimal refLon = branchAddress.Longitude ?? 0;

            // ---------- Gom nhóm đơn theo khu vực ----------
            var areaDict = new Dictionary<string, List<ConfirmedOrderInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (var order in orders)
            {
                // 4.1 – đếm số lần DELIVERYFAILED
                int declineCnt = order.Orderstatushistories.Count(h =>
                     string.Equals(h.Status?.Trim(),
                                   OrderStatusEnum.DELIVERYFAILED.ToString(),
                                   StringComparison.OrdinalIgnoreCase));

                var lat = order.Pickuplatitude ?? 0;
                var lon = order.Pickuplongitude ?? 0;

                // tìm tên quận
                var district = await _mapboxService.GetDistrictFromCoordinatesAsync(lat, lon);
                district = district?.Trim() ?? string.Empty;

                // xác định tên khu vực
                var areaName = districtToArea.TryGetValue(district, out var a) ? a : "Unknown";

                // tính khoảng cách tới chi nhánh
                double distance = _mapboxService.CalculateDistance(lat, lon, refLat, refLon);

                // Convert thời gian UTC sang VN
                var createdAtVn = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow);
                DateTime? pickupTimeVn = order.Pickuptime.HasValue
                                         ? _util.ConvertToVnTime(order.Pickuptime.Value)
                                         : null;

                //Tạo model
                var info = new ConfirmedOrderInfo
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
                    PickupLatitude = lat,
                    PickupLongitude = lon,
                    PickupTime = pickupTimeVn,
                    CreatedAt = createdAtVn,
                    TotalPrice = order.Totalprice,
                    UserDeclineCount = declineCnt
                };

                if (!areaDict.ContainsKey(areaName))
                    areaDict[areaName] = new List<ConfirmedOrderInfo>();

                areaDict[areaName].Add(info);
            }

            // ---------- Chuẩn bị kết quả ----------
            var result = areaDict
                .Where(kv => kv.Value.Any())
                .Select(kv => new AreaOrdersResponse
                {
                    Area = kv.Key,
                    Orders = kv.Value.OrderBy(i => i.PickupTime ?? DateTime.MaxValue) // xa nhất → gần nhất
                                    .ToList()
                })
                .OrderBy(r => r.Area)
                .ToList();

            return result;
        }

        public async Task AssignDeliveryToDriverAsync(HttpContext httpContext, AssignPickupRequest request)
        {
            var adminUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            if (request.DriverId == Guid.Empty)
                throw new ArgumentException("DriverId is required.");

            if (request.OrderIds == null || request.OrderIds.Count == 0)
                throw new ArgumentException("OrderIds cannot be empty.");

            var driver = await _unitOfWork.Repository<User>()
                                          .GetAsync(u => u.Userid == request.DriverId && u.Role == "Driver");
            if (driver == null)
            {
                throw new KeyNotFoundException(
                    $"Không tìm thấy Driver với ID: {request.DriverId} hoặc người này không phải Driver."
                );
            }

            await _unitOfWork.BeginTransaction();
            try
            {
                foreach (var orderId in request.OrderIds)
                {
                    var order = _unitOfWork.Repository<Order>()
                        .GetAll()
                        .FirstOrDefault(o => o.Orderid == orderId);

                    if (order == null)
                        throw new KeyNotFoundException($"OrderId '{orderId}' không tồn tại.");

                    // Tạo row Orderassignmenthistory
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
                    // Nếu order chưa có status history là "SCHEDULED_DELIVERY" hoặc "DELIVERING"
                    if (order.Currentstatus != OrderStatusEnum.SCHEDULED_DELIVERY.ToString() &&
                        order.Currentstatus != OrderStatusEnum.DELIVERING.ToString())
                    {
                        // tạo row Orderstatushistory
                        var statusHistory = new Orderstatushistory
                        {
                            Orderid = orderId,
                            Status = OrderStatusEnum.SCHEDULED_DELIVERY.ToString(),
                            Statusdescription = "Đã lên lịch giao hàng. Bạn sẽ sớm nhận được đơn hàng.",
                            Updatedby = adminUserId,
                            Createdat = DateTime.UtcNow
                        };
                        await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(statusHistory, saveChanges: false);

                        // cập nhật order.currentstatus = "SCHEDULED_DELIVERY"
                        order.Currentstatus = OrderStatusEnum.SCHEDULED_DELIVERY.ToString();
                        await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task DeleteOrderAsync(string orderId)
        {
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
                    .Include(x => x.Orderphotos)
                    .Where(x => x.Orderid == orderId)
                    .ToList();

                // Xoá ảnh (OrderPhotos) trên Backblaze + DB
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

                // Xoá OrderStatusHistory
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

                // lấy tất cả OrderExtras => xóa
                var orderExtras = _unitOfWork.Repository<Orderextra>()
                    .GetAll()
                    .Where(oe => orderItemIds.Contains(oe.Orderitemid))
                    .ToList();
                foreach (var oe in orderExtras)
                {
                    await _unitOfWork.Repository<Orderextra>()
                                     .DeleteAsync(oe, saveChanges: false);
                }

                // xoá OrderItems
                var orderItems = _unitOfWork.Repository<Orderitem>()
                    .GetAll()
                    .Where(oi => oi.Orderid == orderId)
                    .ToList();
                foreach (var item in orderItems)
                {
                    await _unitOfWork.Repository<Orderitem>()
                                     .DeleteAsync(item, saveChanges: false);
                }

                // Xoá OrderAssignmentHistory
                var assignmentHistories = _unitOfWork.Repository<Orderassignmenthistory>()
                    .GetAll()
                    .Where(ah => ah.Orderid == orderId)
                    .ToList();
                foreach (var ah in assignmentHistories)
                {
                    await _unitOfWork.Repository<Orderassignmenthistory>()
                                     .DeleteAsync(ah, saveChanges: false);
                }

                // Xoá Complaints
                var complaints = _unitOfWork.Repository<Complaint>()
                    .GetAll()
                    .Where(com => com.Orderid == orderId)
                    .ToList();
                foreach (var com in complaints)
                {
                    await _unitOfWork.Repository<Complaint>()
                                     .DeleteAsync(com, saveChanges: false);
                }

                // xoá Payments
                var payments = _unitOfWork.Repository<Payment>()
                    .GetAll()
                    .Where(p => p.Orderid == orderId)
                    .ToList();
                foreach (var pay in payments)
                {
                    await _unitOfWork.Repository<Payment>()
                                     .DeleteAsync(pay, saveChanges: false);
                }

                //Xoá DriverLocationHistory
                var driverLoc = _unitOfWork.Repository<Driverlocationhistory>()
                    .GetAll()
                    .Where(d => d.Orderid == orderId)
                    .ToList();
                foreach (var dlh in driverLoc)
                {
                    await _unitOfWork.Repository<Driverlocationhistory>()
                                     .DeleteAsync(dlh, saveChanges: false);
                }

                // Xoá Ratings
                var ratings = _unitOfWork.Repository<Rating>()
                    .GetAll()
                    .Where(r => r.Orderid == orderId)
                    .ToList();
                foreach (var r in ratings)
                {
                    await _unitOfWork.Repository<Rating>()
                                     .DeleteAsync(r, saveChanges: false);
                }

                // Xoá OrderDiscounts
                var orderDiscounts = _unitOfWork.Repository<Orderdiscount>()
                    .GetAll()
                    .Where(od => od.Orderid == orderId)
                    .ToList();
                foreach (var od in orderDiscounts)
                {
                    await _unitOfWork.Repository<Orderdiscount>()
                                     .DeleteAsync(od, saveChanges: false);
                }

                // Xoá Order
                await _unitOfWork.Repository<Order>().DeleteAsync(order, saveChanges: false);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
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

        public async Task CancelAssignmentAsync(HttpContext httpContext, CancelAssignmentRequest request)
        {

            if (request.AssignmentIds is null || request.AssignmentIds.Count == 0)
                throw new ArgumentException("AssignmentIds cannot be empty.");

            await _unitOfWork.BeginTransaction();
            try
            {
                foreach (var assignmentId in request.AssignmentIds)
                {
                    // Lấy assignment
                    var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                        .GetAll()
                        .FirstOrDefault(a => a.Assignmentid == assignmentId);

                    if (assignment is null)
                        throw new KeyNotFoundException($"Assignment {assignmentId} không tồn tại.");

                    // Lấy Order tương ứng
                    var order = _unitOfWork.Repository<Order>()
                                    .GetAll()
                                    .FirstOrDefault(o => o.Orderid == assignment.Orderid);
                    if (order is null)
                        throw new KeyNotFoundException($"Order {assignment.Orderid} không tồn tại.");

                    // Điều kiện huỷ: assignment.Status phải ở ASSIGNED_PICKUP/DELIVERY
                    if (!(assignment.Status == AssignStatusEnum.ASSIGNED_PICKUP.ToString() ||
                          assignment.Status == AssignStatusEnum.ASSIGNED_DELIVERY.ToString()))
                        throw new ApplicationException($"Assignment {assignmentId}: trạng thái không hợp lệ để huỷ (hiện tại: {assignment.Status}).");

                    // Không được có status PICKINGUP trong Orderstatushistory
                    bool existsPickingUp = _unitOfWork.Repository<Orderstatushistory>()
                        .GetAll()
                        .Any(s => s.Orderid == order.Orderid && s.Status == OrderStatusEnum.PICKINGUP.ToString());
                    if (existsPickingUp)
                        throw new ApplicationException($"Order {order.Orderid}: tài xế đã bắt đầu lấy hàng (PICKINGUP), không thể huỷ.");

                    // Xoá assignment
                    await _unitOfWork.Repository<Orderassignmenthistory>()
                        .DeleteAsync(assignment, saveChanges: false);

                    // Xoá status "SCHEDULED_*" liên quan
                    var scheduledStatuses = _unitOfWork.Repository<Orderstatushistory>()
                        .GetAll()
                        .Where(s => s.Orderid == order.Orderid &&
                               (s.Status == OrderStatusEnum.SCHEDULED_PICKUP.ToString() ||
                                s.Status == OrderStatusEnum.SCHEDULED_DELIVERY.ToString()))
                        .ToList();
                    foreach (var sh in scheduledStatuses)
                        await _unitOfWork.Repository<Orderstatushistory>()
                            .DeleteAsync(sh, saveChanges: false);

                    // Cập nhật Order.Currentstatus
                    order.Currentstatus = OrderStatusEnum.CONFIRMED.ToString();
                    await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        //Admin xem các đơn giặt lỗi (isFail trong OrderStatusHistory là true)
        public async Task<List<UserOrderResponse>> GetFailOrdersAsync()
        {
            // Lấy tất cả order maf OrderStatusHistory có isFail = true
            var failOrders = _unitOfWork.Repository<Order>()
                .GetAll()
                .Where(o => o.Orderstatushistories.Any(s => s.Isfail == true))
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                        .ThenInclude(s => s.Subservice)
                            .ThenInclude(sb => sb.Category)
                .OrderByDescending(o => o.Createdat)
                .ToList();

            var result = new List<UserOrderResponse>();

            foreach (var order in failOrders)
            {
                // "OrderName": lấy danh mục (category) của từng service -> gộp bằng dấu phẩy
                //   Mỗi OrderItem -> Service -> Subservice -> Category -> Name
                //   Lọc null + distinct => ghép lại
                var categoryNames = order.Orderitems
                    .Select(oi => oi.Service?.Subservice?.Category?.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                // Gộp thành 1 chuỗi, vd: "Giặt giày, Giặt sấy"
                var orderName = string.Join(", ", categoryNames);

                // Số lượng service = số dòng orderItem
                var serviceCount = order.Orderitems.Count;

                var item = new UserOrderResponse
                {
                    OrderId = order.Orderid,
                    OrderName = orderName,
                    ServiceCount = serviceCount,
                    TotalPrice = order.Totalprice,
                    OrderedDate = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
                    OrderStatus = order.Currentstatus
                };

                result.Add(item);
            }

            return result;
        }
        public async Task<List<DriverCashDailyResponse>> GetDriverCashDailyAsync(DateTime date)
        {
            var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var end = start.AddDays(1);

            var assignmentsQ = _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Where(a => a.Assignedat >= start && a.Assignedat < end
                            && a.Assignedto != null)
                .Select(a => new
                {
                    a.Orderid,
                    DriverId = a.Assignedto,
                    a.Assignedat
                });

            var assignments = await assignmentsQ.ToListAsync();

            var orderIdsInDay = assignments.Select(a => a.Orderid).Distinct().ToList();

            var cashPayments = await _unitOfWork.Repository<Payment>()
                .GetAll()
                .Include(p => p.Paymentmethod)
                .Where(p => p.Paymentmethod.Name == "Cash"
                            && orderIdsInDay.Contains(p.Orderid)
                            && p.Collectedby != null)
                .ToListAsync();

            var orderToDriver = assignments
                .GroupBy(a => a.Orderid)
                .ToDictionary(g => g.Key, g => g.First().DriverId);

            var driverIds = orderToDriver.Values.Distinct().ToList();

            var drivers = await _unitOfWork.Repository<User>()
                .GetAll()
                .Where(u => driverIds.Contains(u.Userid))
                .ToDictionaryAsync(u => u.Userid);

            var result = cashPayments
                .GroupBy(p => orderToDriver[p.Orderid])
                .Select(g =>
                {
                    var driver = drivers[g.Key];
                    var totalCollected = g.Sum(p => p.Amount);

                    var returned = g.Where(p => p.Isreturnedtoadmin).ToList();
                    var returnedAmount = returned.Sum(p => p.Amount);
                    var returnedOrders = returned.Select(p => p.Orderid).Distinct().Count();

                    var unreturnedAmount = totalCollected - returnedAmount;

                    return new DriverCashDailyResponse
                    {
                        DriverId = g.Key,
                        DriverName = driver.Fullname ?? "(No name)",
                        DriverAvatar = driver.Avatar,
                        DriverPhone = driver.Phonenumber,
                        CashOrdersCount = g.Select(p => p.Orderid).Distinct().Count(),
                        ReturnedOrdersCount = returnedOrders,
                        TotalCollectedAmount = totalCollected,
                        TotalReturnedAmount = returnedAmount,
                        TotalUnreturnedAmount = unreturnedAmount
                    };
                })
                .OrderBy(r => r.DriverName)
                .ToList();

            return result;
        }

        public async Task<List<DriverCashOrderResponse>> GetDriverCashOrdersAsync(Guid driverId, DateTime date)
        {
            var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var end = start.AddDays(1);

            var assignQ = _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Where(a =>
                    a.Assignedto == driverId &&
                    a.Assignedat >= start && a.Assignedat < end &&
                    a.Status == AssignStatusEnum.DELIVERY_SUCCESS.ToString());

            var data = from a in assignQ
                       join p in _unitOfWork.Repository<Payment>().GetAll()
                                .Include(pay => pay.Paymentmethod)
                            on a.Orderid equals p.Orderid
                       where p.Paymentmethod.Name == "Cash"
                       select new DriverCashOrderResponse
                       {
                           PaymentId = p.Paymentid,
                           OrderId = p.Orderid,
                           Amount = p.Amount,
                           AssignedAt = a.Assignedat,
                           PaymentDate = p.Paymentdate,
                           UpdatedAt = p.Updatedat,
                           IsReturnedToAdmin = p.Isreturnedtoadmin
                       };

            return await data
                .OrderBy(d => d.AssignedAt)
                .ThenBy(d => d.PaymentId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task MarkCashReturnedAsync(List<string> orderIds)
        {
            if (orderIds == null || orderIds.Count == 0)
                throw new ArgumentException("Danh sách OrderId trống.");

            var payments = await _unitOfWork.Repository<Payment>().GetAll()
                .Include(p => p.Paymentmethod)
                .Where(p =>
                    orderIds.Contains(p.Orderid) &&
                    p.Paymentmethod.Name == "Cash" &&
                    !p.Isreturnedtoadmin)
                .ToListAsync();

            if (payments.Count == 0)
                throw new InvalidOperationException("Không tìm thấy khoản tiền mặt nào cần cập nhật.");

            foreach (var pay in payments)
            {
                pay.Isreturnedtoadmin = true;
                pay.Updatedat = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
