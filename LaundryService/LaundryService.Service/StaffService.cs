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

        public StaffService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
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
    }
}
