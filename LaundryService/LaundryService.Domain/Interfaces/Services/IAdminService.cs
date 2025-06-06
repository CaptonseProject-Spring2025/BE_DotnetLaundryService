﻿using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IAdminService
    {
        Task<List<AreaOrdersResponse>> GetConfirmedOrdersByAreaAsync();

        Task AssignPickupToDriverAsync(HttpContext httpContext, AssignPickupRequest request);

        Task<Guid> GetCustomerIdByOrderAsync(string orderId);

        Task<List<AreaOrdersResponse>> GetQualityCheckedOrdersByAreaAsync();

        Task AssignDeliveryToDriverAsync(HttpContext httpContext, AssignPickupRequest request);

        Task DeleteOrderAsync(string orderId);

        Task DeleteOrdersAsync(List<string> orderIds);

        Task CancelAssignmentAsync(HttpContext httpContext, CancelAssignmentRequest request);

        //Admin xem các đơn giặt lỗi (isFail trong OrderStatusHistory là false)
        Task<List<UserOrderResponse>> GetFailOrdersAsync();
        Task<List<DriverCashDailyResponse>> GetDriverCashDailyAsync(DateTime date);

        Task<List<DriverCashOrderResponse>> GetDriverCashOrdersAsync(Guid driverId, DateTime date);

        Task MarkCashReturnedAsync(List<string> orderIds);
    }
}
