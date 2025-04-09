using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaundryService.Service
{
    public class StaffService : IStaffService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IFileStorageService _fileStorageService;

        public StaffService(IUnitOfWork unitOfWork, IUtil util, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _fileStorageService = fileStorageService;
        }

        /// <summary>
        /// Lấy tất cả đơn có currentStatus = PICKEDUP 
        /// và có OrderAssignmentHistory (mới nhất) = PICKUP_SUCCESS,
        /// sắp xếp theo Emergency desc, rồi Deliverytime asc.
        /// </summary>
        public async Task<List<PickedUpOrderResponse>> GetPickedUpOrdersAsync(HttpContext httpContext)
        {
            // (1) Xác thực Staff (nếu cần), tạm thời coi như [Authorize(Roles="Staff")] ở Controller
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            // (2) Truy vấn Order
            //    - currentStatus == "PICKEDUP"
            //    - Có ít nhất 1 assignmenthistory với Status="PICKUP_SUCCESS"
            //    - Lấy Eager load: .Include(o => o.User), .Include(o => o.Orderitems => Service)
            //    - Rồi sắp xếp theo Emergency desc, Deliverytime asc

            var ordersQuery = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.User)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                .Include(o => o.Orderassignmenthistories)
                .Where(o =>
                    o.Currentstatus == OrderStatusEnum.PICKEDUP.ToString()
                    && o.Orderassignmenthistories.Any(ah =>
                           ah.Status == AssignStatusEnum.PICKUP_SUCCESS.ToString()
                       // Bạn có thể kiểm tra "mới nhất" = so sánh ah.Completedat == max ...
                       // .OrderByDescending(ah => ah.Completedat).FirstOrDefault() 
                       // Tùy logic, ở đây chỉ cần "có" 1 record PICKUP_SUCCESS
                       )
                );

            // (3) Lấy dữ liệu, sắp xếp:
            //     - Emergency desc (true trước)
            //     - Deliverytime asc
            // Lưu ý: EF không thể OrderBy 2 field kiểu bool desc + Date asc 
            //        => ta tách .OrderByDescending(...) xong .ThenBy(...)

            var orders = ordersQuery
                .OrderByDescending(o => o.Emergency ?? false)      // null => false
                .ThenBy(o => o.Deliverytime) // asc
                .ToList(); // load về memory

            // (4) Map -> PickedUpOrderResponse
            var result = new List<PickedUpOrderResponse>();

            foreach (var o in orders)
            {
                // Gom serviceNames
                var serviceNames = o.Orderitems
                    .Select(oi => oi.Service?.Name ?? "Unknown")
                    .Distinct()
                    .ToList();
                var joinedServiceNames = string.Join("; ", serviceNames);

                // Số service
                int serviceCount = o.Orderitems.Count;

                // Chuyển Pickuptime, Deliverytime sang giờ VN
                var pickupTimeVn = o.Pickuptime.HasValue
                    ? _util.ConvertToVnTime(o.Pickuptime.Value)
                    : (DateTime?)null;

                var deliveryTimeVn = o.Deliverytime.HasValue
                    ? _util.ConvertToVnTime(o.Deliverytime.Value)
                    : (DateTime?)null;

                // Tạo response object
                var orderResp = new PickedUpOrderResponse
                {
                    OrderId = o.Orderid,
                    Emergency = (o.Emergency ?? false), // null => false
                    CustomerInfo = new CustomerInfoDto
                    {
                        CustomerId = o.Userid,
                        CustomerName = o.User?.Fullname,
                        CustomerPhone = o.User?.Phonenumber
                    },
                    ServiceNames = joinedServiceNames,
                    ServiceCount = serviceCount,
                    PickupTime = pickupTimeVn,
                    DeliveryTime = deliveryTimeVn,
                    CurrentStatus = o.Currentstatus ?? "",
                    OrderDate = _util.ConvertToVnTime(o.Createdat ?? DateTime.UtcNow),
                    TotalPrice = o.Totalprice
                };

                result.Add(orderResp);
            }

            return result;
        }

        /// <summary>
        /// Staff nhận đơn (đơn đang PICKEDUP) để kiểm tra/giặt => chuyển Order sang CHECKING.
        /// </summary>
        public async Task ReceiveOrderForCheckAsync(HttpContext httpContext, string orderId)
        {
            // 1) Lấy userId (Staff) từ token
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Tìm Order => check status = "PICKEDUP"
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Orderid == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy OrderId: {orderId}");
            }

            // Kiểm tra trạng thái có phải PICKEDUP không
            if (order.Currentstatus != OrderStatusEnum.PICKEDUP.ToString())
            {
                // Nếu không đúng => throw
                throw new ApplicationException("Đơn hàng không ở trạng thái PICKEDUP hoặc đang được xử lý bởi Staff khác.");
            }

            // 3) Cập nhật Order => "CHECKING"
            order.Currentstatus = OrderStatusEnum.CHECKING.ToString();
            await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

            // 4) Tạo Orderstatushistory
            var history = new Orderstatushistory
            {
                Orderid = orderId,
                Status = OrderStatusEnum.CHECKING.ToString(),
                Statusdescription = "Đơn hàng đang được kiểm tra",
                Updatedby = staffId,
                Createdat = DateTime.UtcNow
            };
            await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(history, saveChanges: false);

            // 5) Lưu DB
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Lấy danh sách các đơn có Currentstatus = "CHECKING", 
        /// mà chính Staff này đã cập nhật (Orderstatushistory.Updatedby = staffId).
        /// </summary>
        public async Task<List<PickedUpOrderResponse>> GetCheckingOrdersAsync(HttpContext httpContext)
        {
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);
            var checkingStatus = OrderStatusEnum.CHECKING.ToString();

            // 1) Tìm tất cả orderId trong Orderstatushistory 
            //    có Status = "CHECKING" và Updatedby = staffId.
            var orderIdsStaffUpdated = _unitOfWork.Repository<Orderstatushistory>()
                .GetAll()
                .Where(h => h.Status == checkingStatus && h.Updatedby == staffId)
                .Select(h => h.Orderid)
                .Distinct()
                .ToList();

            if (!orderIdsStaffUpdated.Any())
            {
                // Không có order nào staff này cập nhật sang CHECKING
                return new List<PickedUpOrderResponse>();
            }

            // 2) Lấy danh sách Orders thỏa mãn:
            //    - OrderId ∈ orderIdsStaffUpdated
            //    - Currentstatus == "CHECKING"
            var orders = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.User)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                .Where(o => o.Currentstatus == checkingStatus
                            && orderIdsStaffUpdated.Contains(o.Orderid))
                .ToList();

            // 3) Map Orders -> CheckingOrderResponse
            var result = new List<PickedUpOrderResponse>();
            foreach (var order in orders)
            {
                // Gom serviceNames
                var serviceNames = order.Orderitems
                    .Select(oi => oi.Service?.Name ?? "Unknown")
                    .Distinct()
                    .ToList();
                var joinedServiceNames = string.Join("; ", serviceNames);

                // Tạo DTO
                var dto = new PickedUpOrderResponse
                {
                    OrderId = order.Orderid,
                    CustomerInfo = new CustomerInfoDto
                    {
                        CustomerId = order.Userid,
                        CustomerName = order.User?.Fullname,
                        CustomerPhone = order.User?.Phonenumber
                    },
                    ServiceNames = joinedServiceNames,
                    ServiceCount = order.Orderitems.Count,
                    OrderDate = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
                    PickupTime = order.Pickuptime.HasValue
                                ? _util.ConvertToVnTime(order.Pickuptime.Value)
                                : (DateTime?)null,
                    DeliveryTime = order.Deliverytime.HasValue
                                ? _util.ConvertToVnTime(order.Deliverytime.Value)
                                : (DateTime?)null,
                    CurrentStatus = order.Currentstatus,
                    Emergency = order.Emergency ?? false, // Nếu null => false
                    TotalPrice = order.Totalprice
                };
                result.Add(dto);
            }

            return result;
        }

        public async Task<CheckingOrderUpdateResponse> UpdateCheckingOrderAsync(
            HttpContext httpContext,
            string orderId,
            string? notes,
            IFormFileCollection? files
        )
        {
            // 1) Lấy staffId từ token
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Tìm OrderStatusHistory => Status = "CHECKING", Orderid = orderId
            //    Lấy row 'mới nhất' hay 'duy nhất'? Thường 1 status = checking
            var checkingRow = _unitOfWork.Repository<Orderstatushistory>()
                .GetAll()
                .Where(h => h.Orderid == orderId && h.Status == OrderStatusEnum.CHECKING.ToString())
                .OrderByDescending(h => h.Createdat)
                .FirstOrDefault();

            if (checkingRow == null)
                throw new KeyNotFoundException("Không tìm thấy đơn đang ở trạng thái CHECKING.");

            // 3) Kiểm tra Updatedby có bằng staffId hay không
            if (checkingRow.Updatedby != staffId)
                throw new ApplicationException("Bạn không phải là người nhận xử lý đơn hàng này.");

            // 4) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            var listPhotoUrls = new List<string>();
            try
            {
                // 5) Nếu có notes => update
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    checkingRow.Notes = notes; // cập nhật notes
                    // Mark entity => or call UpdateAsync
                    _unitOfWork.DbContext.Entry(checkingRow).State = EntityState.Modified;
                }

                // 6) Nếu có files => upload
                if (files != null && files.Count > 0)
                {
                    // Gọi hàm upload nhiều file -> 
                    var uploadResult = await _fileStorageService.UploadMultipleFilesAsync(files, "order-photos");

                    // Nếu muốn *fail entire transaction* nếu có bất kỳ file fail:
                    if (uploadResult.FailureCount > 0)
                    {
                        // rollback transaction => ném exception
                        var firstError = uploadResult.FailedUploads.First().ErrorMessage;
                        throw new ApplicationException($"Đã upload thất bại 1 hoặc nhiều ảnh. Lỗi đầu tiên: {firstError}");
                    }

                    // Các file thành công => insert record trong Orderphoto
                    foreach (var suc in uploadResult.SuccessfulUploads)
                    {
                        var newPhoto = new Orderphoto
                        {
                            Statushistoryid = checkingRow.Statushistoryid,
                            Photourl = suc.Url
                        };
                        await _unitOfWork.Repository<Orderphoto>().InsertAsync(newPhoto, saveChanges: false);
                        listPhotoUrls.Add(suc.Url);
                    }
                }

                // 7) Save + commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                // Tạo response
                var response = new CheckingOrderUpdateResponse
                {
                    OrderId = orderId,
                    Notes = checkingRow.Notes,
                    PhotoUrls = listPhotoUrls
                };
                return response;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task ConfirmCheckingDoneAsync(HttpContext httpContext, string orderId, string notes)
        {
            // 1) Lấy staffId từ JWT
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Tìm order => check currentstatus == CHECKING
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Orderid == orderId);

            if (order == null)
                throw new KeyNotFoundException($"Order not found: {orderId}");

            if (order.Currentstatus != OrderStatusEnum.CHECKING.ToString())
                throw new ApplicationException($"Order {orderId} is not in CHECKING status.");

            // 3) Kiểm tra xem Staff này có phải người xử lý CHECKING không?
            //    Tức row Orderstatushistory mới nhất => Status=CHECKING => Updatedby=staffId
            var checkingRow = _unitOfWork.Repository<Orderstatushistory>()
                .GetAll()
                .Where(h => h.Orderid == orderId && h.Status == OrderStatusEnum.CHECKING.ToString())
                .OrderByDescending(h => h.Createdat)
                .FirstOrDefault();

            if (checkingRow == null)
                throw new KeyNotFoundException("Không tìm thấy history CHECKING cho order này.");

            if (checkingRow.Updatedby != staffId)
                throw new ApplicationException("Bạn không phải là người xử lý đơn CHECKING này.");

            // 4) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                // 5) Update Order => CHECKED
                order.Currentstatus = OrderStatusEnum.CHECKED.ToString();
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                // 6) Thêm Orderstatushistory => Status = CHECKED
                var newHistory = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = OrderStatusEnum.CHECKED.ToString(),
                    Statusdescription = "Đơn hàng đã được kiểm tra và sẽ sớm được mang đi giặt",
                    Notes = notes, // Ghi chú (có thể rỗng)
                    Updatedby = staffId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(newHistory, saveChanges: false);

                // 7) Lưu DB + commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<List<PickedUpOrderResponse>> GetCheckedOrdersAsync(HttpContext httpContext)
        {
            // 1) Xác thực staff
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Tạo query: Lấy tất cả Orders có Currentstatus = "CHECKED"
            //    Eager load: .Include(o => o.User), .Include(o => o.Orderitems -> Service)
            var checkedStatus = OrderStatusEnum.CHECKED.ToString();

            var query = _unitOfWork.Repository<Order>()
                .GetAll()
                .Include(o => o.User)
                .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.Service)
                .Where(o => o.Currentstatus == checkedStatus);

            // 3) Sắp xếp:
            //    - Emergency desc
            //    - DeliveryTime asc
            var orders = query
                .OrderByDescending(o => o.Emergency ?? false)
                .ThenBy(o => o.Deliverytime)
                .ToList();

            // 4) Map -> PickedUpOrderResponse
            var result = new List<PickedUpOrderResponse>();

            foreach (var order in orders)
            {
                // Gom serviceNames (VD: "Giặt áo; Giặt quần")
                var serviceNames = order.Orderitems
                    .Select(oi => oi.Service?.Name ?? "Unknown")
                    .Distinct()
                    .ToList();
                var joinedServiceNames = string.Join("; ", serviceNames);

                // Tính giờ Việt Nam
                DateTime? pickupTimeVn = order.Pickuptime.HasValue
                    ? _util.ConvertToVnTime(order.Pickuptime.Value)
                    : (DateTime?)null;

                DateTime? deliveryTimeVn = order.Deliverytime.HasValue
                    ? _util.ConvertToVnTime(order.Deliverytime.Value)
                    : (DateTime?)null;

                // Tạo DTO
                var dto = new PickedUpOrderResponse
                {
                    OrderId = order.Orderid,
                    Emergency = order.Emergency ?? false,
                    CustomerInfo = new CustomerInfoDto
                    {
                        CustomerId = order.Userid,
                        CustomerName = order.User?.Fullname,
                        CustomerPhone = order.User?.Phonenumber
                    },
                    ServiceNames = joinedServiceNames,
                    ServiceCount = order.Orderitems.Count,
                    OrderDate = _util.ConvertToVnTime(order.Createdat ?? DateTime.UtcNow),
                    PickupTime = pickupTimeVn,
                    DeliveryTime = deliveryTimeVn,
                    CurrentStatus = order.Currentstatus ?? "",
                    TotalPrice = order.Totalprice
                };

                result.Add(dto);
            }

            return result;
        }

        public async Task<Guid> ReceiveOrderForWashingAsync(
            HttpContext httpContext,
            string orderId,
            string? notes,
            IFormFileCollection? files
        )
        {
            // 1) Lấy StaffId từ JWT
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            // 2) Lấy Order => check status = CHECKED
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Orderid == orderId);

            if (order == null)
                throw new KeyNotFoundException($"Order not found: {orderId}");

            if (order.Currentstatus != OrderStatusEnum.CHECKED.ToString())
            {
                // Không hợp lệ
                throw new ApplicationException(
                    "Đơn hàng không ở trạng thái hợp lệ hoặc đang được xử lý bởi Staff khác (Cần CHECKED)."
                );
            }

            // 3) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                // 4) Cập nhật Order => WASHING
                order.Currentstatus = OrderStatusEnum.WASHING.ToString();
                await _unitOfWork.Repository<Order>().UpdateAsync(order, saveChanges: false);

                // 5) Tạo 1 Orderstatushistory => WASHING
                var newHistory = new Orderstatushistory
                {
                    Orderid = orderId,
                    Status = OrderStatusEnum.WASHING.ToString(),
                    Statusdescription = "Đơn hàng đang được thực hiện.",
                    Updatedby = staffId,
                    Notes = notes,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(newHistory, saveChanges: false);

                // Lưu tạm DB để lấy Statushistoryid của record mới (nếu cần).
                await _unitOfWork.SaveChangesAsync();

                // 6) Nếu có files => Upload -> tạo record Orderphoto
                if (files != null && files.Count > 0)
                {
                    // Upload nhiều file
                    var uploadResult = await _fileStorageService.UploadMultipleFilesAsync(files, "order-photos");

                    // Kiểm tra nếu có file fail => rollback transaction
                    if (uploadResult.FailureCount > 0)
                    {
                        var firstError = uploadResult.FailedUploads.First().ErrorMessage;
                        throw new ApplicationException($"Upload ảnh thất bại. Lỗi đầu tiên: {firstError}");
                    }

                    // Tạo Orderphoto cho các file thành công
                    foreach (var suc in uploadResult.SuccessfulUploads)
                    {
                        var photo = new Orderphoto
                        {
                            Statushistoryid = newHistory.Statushistoryid, // liên kết với record mới
                            Photourl = suc.Url
                        };
                        await _unitOfWork.Repository<Orderphoto>().InsertAsync(photo, saveChanges: false);
                    }
                }

                // 7) Lưu và commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                // 8) Trả về Statushistoryid mới
                return newHistory.Statushistoryid;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
