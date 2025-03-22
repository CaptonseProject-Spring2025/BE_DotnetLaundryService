using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Lấy userId từ JWT token. Ném exception nếu không hợp lệ.
        /// </summary>
        private Guid GetCurrentUserIdOrThrow(HttpContext httpContext)
        {
            var userIdClaim = httpContext?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
            {
                return userId;
            }
            throw new UnauthorizedAccessException("Invalid token: Cannot retrieve userId.");
        }

        public async Task AddToCartAsync(HttpContext httpContext, AddToCartRequest request)
        {
            // 1) Lấy userId từ token
            var userId = GetCurrentUserIdOrThrow(httpContext);

            // 2) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();

            try
            {
                // 3) Tìm ServiceDetail
                var serviceDetail = await _unitOfWork
                    .Repository<Servicedetail>()
                    .GetAsync(s => s.Serviceid == request.ServiceDetailId);

                if (serviceDetail == null)
                    throw new KeyNotFoundException("Service detail not found.");

                // 4) Tìm Order INCART
                var order = _unitOfWork.Repository<Order>()
                    .GetAll()
                    .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

                if (order == null)
                {
                    // Chưa có => tạo mới
                    order = new Order
                    {
                        Userid = userId,
                        Currentstatus = "INCART",
                        Createdat = DateTime.Now
                    };
                    await _unitOfWork.Repository<Order>().InsertAsync(order);
                    await _unitOfWork.SaveChangesAsync();
                }

                // 5) Kiểm tra xem ExtraIds có hợp lệ không
                var validExtras = new List<Extra>();
                if (request.ExtraIds != null && request.ExtraIds.Count > 0)
                {
                    validExtras = _unitOfWork.Repository<Extra>()
                        .GetAll()
                        .Where(e => request.ExtraIds.Contains(e.Extraid))
                        .ToList();

                    var invalidIds = request.ExtraIds.Except(validExtras.Select(x => x.Extraid)).ToList();
                    if (invalidIds.Any())
                    {
                        throw new ApplicationException($"Some extras not found in database: {string.Join(", ", invalidIds)}");
                    }
                }

                // 6) Tìm xem có OrderItem nào trùng ServiceDetail và trùng EXACT Extras
                //    (Sẽ load kèm OrderExtras để so sánh)
                //    Chỉ load những OrderItem có ServiceID == serviceDetail.Serviceid
                //    Rồi so sánh ExtraIds
                var orderItemsSameService = _unitOfWork.Repository<Orderitem>()
                    .GetAll()
                    .Where(oi => oi.Orderid == order.Orderid && oi.Serviceid == serviceDetail.Serviceid)
                    .ToList();

                Orderitem? matchedItem = null;

                foreach (var oi in orderItemsSameService)
                {
                    // Lấy list OrderExtra gắn với OrderItem này
                    var oiExtras = _unitOfWork.Repository<Orderextra>()
                        .GetAll()
                        .Where(e => e.Orderitemid == oi.Orderitemid)
                        .ToList();

                    // So sánh
                    if (ExtrasAreTheSame(oiExtras, request.ExtraIds ?? new List<Guid>()))
                    {
                        matchedItem = oi;
                        break;
                    }
                }

                if (matchedItem != null)
                {
                    // 7) Nếu có matchedItem => tăng Quantity
                    matchedItem.Quantity += request.Quantity;
                    await _unitOfWork.Repository<Orderitem>().UpdateAsync(matchedItem, saveChanges: false);
                }
                else
                {
                    // 8) Nếu không có => tạo OrderItem mới
                    var newOrderItem = new Orderitem
                    {
                        Orderid = order.Orderid,
                        Serviceid = serviceDetail.Serviceid,
                        Quantity = request.Quantity
                    };
                    await _unitOfWork.Repository<Orderitem>().InsertAsync(newOrderItem, saveChanges: false);
                    await _unitOfWork.SaveChangesAsync();

                    // 9) Thêm OrderExtra (nếu có)
                    if (validExtras.Any())
                    {
                        var newOrderExtras = validExtras.Select(ext => new Orderextra
                        {
                            Orderitemid = newOrderItem.Orderitemid,
                            Extraid = ext.Extraid
                        }).ToList();

                        await _unitOfWork.Repository<Orderextra>().InsertRangeAsync(newOrderExtras, saveChanges: false);
                    }
                }

                // 10) Lưu changes & commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                // rollback
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// So sánh xem 2 tập ExtraId có giống hệt nhau không.
        /// </summary>
        private bool ExtrasAreTheSame(ICollection<Orderextra> existingExtras, List<Guid> newExtraIds)
        {
            var existingIds = existingExtras.Select(e => e.Extraid).ToHashSet();
            var newIds = newExtraIds.ToHashSet();

            if (existingIds.Count != newIds.Count)
                return false;

            return existingIds.SetEquals(newIds);
        }

        public async Task<CartResponse> GetCartAsync(HttpContext httpContext)
        {
            var userId = GetCurrentUserIdOrThrow(httpContext);

            // Lấy Order INCART
            var order = _unitOfWork.Repository<Order>()
                .GetAll()
                .FirstOrDefault(o => o.Userid == userId && o.Currentstatus == "INCART");

            if (order == null)
            {
                // Tùy logic: ném lỗi hoặc trả về Cart trống
                throw new KeyNotFoundException("No cart found for current user.");
            }

            // Lấy tất cả OrderItem + OrderExtra của order
            // Muốn Eager Load, ta có thể dùng Include:
            //    .Include(o => o.Orderitems).ThenInclude(oi => oi.Orderextras)
            //    .ThenInclude(oe => oe.Extra)
            // … Hoặc truy vấn rời như dưới

            // Lấy OrderItem
            var orderItems = _unitOfWork.Repository<Orderitem>()
                .GetAll()
                .Where(oi => oi.Orderid == order.Orderid)
                .ToList();

            // Xây CartResponse
            var cartResponse = new CartResponse
            {
                OrderId = order.Orderid
            };

            decimal total = 0;

            foreach (var item in orderItems)
            {
                // Lấy ServiceDetail (để lấy Price, Name)
                var service = _unitOfWork.Repository<Servicedetail>()
                    .GetAll()
                    .FirstOrDefault(s => s.Serviceid == item.Serviceid);

                var serviceId = service.Serviceid;
                var servicePrice = service?.Price ?? 0;
                var serviceName = service?.Name ?? "Unknown";

                // Lấy các Extras
                var orderExtras = _unitOfWork.Repository<Orderextra>()
                    .GetAll()
                    .Where(oe => oe.Orderitemid == item.Orderitemid)
                    .ToList();

                // Map sang CartExtraResponse
                var extraResponses = new List<CartExtraResponse>();
                decimal sumExtraPrices = 0;
                foreach (var oe in orderExtras)
                {
                    // Lấy entity Extra (nếu cần Name, Price)
                    var extraEntity = _unitOfWork.Repository<Extra>()
                        .GetAll()
                        .FirstOrDefault(e => e.Extraid == oe.Extraid);

                    var extraPrice = extraEntity?.Price ?? 0;
                    var extraName = extraEntity?.Name ?? "Unknown Extra";

                    extraResponses.Add(new CartExtraResponse
                    {
                        ExtraId = oe.Extraid,
                        ExtraName = extraName,
                        ExtraPrice = extraPrice
                    });

                    sumExtraPrices += extraPrice;
                }

                // Tính SubTotal = (servicePrice + sumExtraPrices) * quantity
                var subTotal = (servicePrice + sumExtraPrices) * item.Quantity;

                // Cộng vào total giỏ
                total += subTotal;

                // Tạo CartItemResponse
                var cartItem = new CartItemResponse
                {
                    OrderItemId = item.Orderitemid,
                    ServiceId = serviceId,
                    ServiceName = serviceName,
                    ServicePrice = servicePrice,
                    Quantity = item.Quantity,
                    Extras = extraResponses,
                    SubTotal = subTotal
                };

                cartResponse.Items.Add(cartItem);
            }

            cartResponse.EstimatedTotal = total;
            return cartResponse;
        }

    }
}

        /// <summary>
        /// Build OrderResponse để trả về client sau khi thêm sản phẩm thành công.
        /// </summary>
        //private async Task<OrderResponse> BuildOrderResponse(Guid orderId)
        //{
        //    var order = await _unitOfWork.Repository<Order>()
        //        .GetAsync(o => o.Orderid == orderId, includeProperties: "Orderitems,Orderitems.Orderextras");

        //    if (order == null)
        //        throw new KeyNotFoundException("Order not found after creation.");

        //    var response = new OrderResponse
        //    {
        //        OrderId = order.Orderid,
        //        CurrentStatus = order.Currentstatus,
        //        CreatedAt = order.Createdat
        //    };

        //    // Lấy danh sách serviceDetailId -> serviceName
        //    var serviceDetailMap = _unitOfWork.Repository<Servicedetail>()
        //        .GetAll()
        //        .ToDictionary(s => s.Serviceid, s => s.Name);

        //    // Lấy danh sách extraId -> extraName
        //    var extraMap = _unitOfWork.Repository<Extra>()
        //        .GetAll()
        //        .ToDictionary(e => e.Extraid, e => e.Name);

        //    foreach (var item in order.Orderitems)
        //    {
        //        var itemResp = new OrderItemResponse
        //        {
        //            OrderItemId = item.Orderitemid,
        //            ServiceId = item.Serviceid,
        //            ServiceName = serviceDetailMap.ContainsKey(item.Serviceid)
        //                           ? serviceDetailMap[item.Serviceid]
        //                           : "Unknown Service",
        //            Quantity = item.Quantity ?? 1,
        //            BasePrice = item.Baseprice
        //        };

        //        // Lấy ra các orderExtras
        //        if (item.Orderextras != null && item.Orderextras.Count > 0)
        //        {
        //            foreach (var oe in item.Orderextras)
        //            {
        //                var oeResp = new OrderExtraResponse
        //                {
        //                    OrderExtraId = oe.Orderextraid,
        //                    ExtraId = oe.Extraid,
        //                    ExtraName = extraMap.ContainsKey(oe.Extraid) ? extraMap[oe.Extraid] : "Unknown Extra",
        //                    ExtraPrice = oe.Extraprice
        //                };
        //                itemResp.Extras.Add(oeResp);
        //            }
        //        }

        //        response.Items.Add(itemResp);
        //    }

        //    return response;
        //}
