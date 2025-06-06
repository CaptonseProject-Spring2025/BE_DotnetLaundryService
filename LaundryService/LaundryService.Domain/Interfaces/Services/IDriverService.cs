﻿using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IDriverService
    {
        Task StartOrderPickupAsync(HttpContext httpContext, string orderId);
        Task ConfirmOrderPickupArrivedAsync(HttpContext httpContext, string orderId);
        Task ConfirmOrderPickedUpAsync(HttpContext httpContext, string orderId, string notes);
        Task CancelAssignedPickupAsync(HttpContext httpContext, string orderId, string cancelReason);
        Task CancelPickupNoShowAsync(HttpContext httpContext, string orderId);
        Task StartOrderDeliveryAsync(HttpContext httpContext, string orderId);
        Task ConfirmOrderDeliveredAsync(HttpContext httpContext, string orderId, string notes);
        Task ConfirmOrderDeliverySuccessAsync(HttpContext httpContext, string orderId);
        Task CancelAssignedDeliveryAsync(HttpContext httpContext, string orderId, string cancelReason);
        Task CancelDeliveryNoShowAsync(HttpContext httpContext, string orderId);
        Task<DriverStatisticsResponse> GetDailyStatisticsAsync(HttpContext httpContext, DateTime date);
        Task<DriverStatisticsResponse> GetWeeklyStatisticsAsync(HttpContext httpContext, DateTime dateInWeek);
        Task<DriverStatisticsResponse> GetMonthlyStatisticsAsync(HttpContext httpContext, int year, int month);
        Task<List<DriverStatisticsListResponse>> GetDailyStatisticsListAsync(HttpContext httpContext, DateTime date);
        Task<List<DriverStatisticsListResponse>> GetWeeklyStatisticsListAsync(HttpContext httpContext, DateTime dateInWeek);
        Task<List<DriverStatisticsListResponse>> GetMonthlyStatisticsListAsync(HttpContext httpContext, int year, int month);
    }
}
