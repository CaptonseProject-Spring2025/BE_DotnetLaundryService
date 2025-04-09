using LaundryService.Dto.Pagination;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IOrderService
    {
        Task AddToCartAsync(HttpContext httpContext, AddToCartRequest request);

        Task<CartResponse> GetCartAsync(HttpContext httpContext);

        Task<string> PlaceOrderAsync(HttpContext httpContext, PlaceOrderRequest request);

        Task<List<UserOrderResponse>> GetUserOrdersAsync(HttpContext httpContext);

        Task<PaginationResult<UserOrderResponse>> GetAllOrdersAsync(HttpContext httpContext, string? status, int page, int pageSize);

        Task<PaginationResult<UserOrderResponse>> GetPendingOrdersForStaffAsync(HttpContext httpContext, int page, int pageSize);

        Task<OrderDetailCustomResponse> GetOrderDetailCustomAsync(HttpContext httpContext, string orderId);

        Task<List<OrderStatusHistoryItemResponse>> GetOrderStatusHistoryAsync(HttpContext httpContext, string orderId);

        Task<PaginationResult<InCartOrderAdminResponse>> GetInCartOrdersPagedAsync(HttpContext httpContext, int page, int pageSize);

        Task<Guid> ProcessOrderAsync(HttpContext httpContext, string orderId);

        Task ConfirmOrderAsync(HttpContext httpContext, string orderId, string notes);

        Task CancelOrderAsync(HttpContext httpContext, Guid assignmentId, string notes);

        Task CancelProcessingAsync(HttpContext httpContext, Guid assignmentId, string note);

        Task<CartResponse> UpdateCartItemAsync(HttpContext httpContext, UpdateCartItemRequest request);

        Task<Guid> GetCustomerIdByOrderAsync(string orderId);

        Task<Guid> GetCustomerIdByAssignmentAsync(Guid assignmentId);

        Task<string> GetOrderIdByAssignmentAsync(Guid assignmentId);

        Task StartOrderPickupAsync(HttpContext httpContext, string orderId);

        Task ConfirmOrderPickedUpAsync(HttpContext httpContext, string orderId, string notes);

        Task ConfirmOrderPickupSuccessAsync(HttpContext httpContext, string orderId);

        Task CancelAssignedPickupAsync(HttpContext httpContext, string orderId, string cancelReason);

        Task StartOrderDeliveryAsync(HttpContext httpContext, string orderId);

        Task ConfirmOrderDeliveredAsync(HttpContext httpContext, string orderId, string notes);

        Task ConfirmOrderDeliverySuccessAsync(HttpContext httpContext, string orderId);

        Task CancelAssignedDeliveryAsync(HttpContext httpContext, string orderId, string cancelReason);
    }
}
