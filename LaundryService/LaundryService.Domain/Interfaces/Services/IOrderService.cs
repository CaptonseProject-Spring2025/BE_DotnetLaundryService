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

        Task<CartResponse> ReorderAsync(HttpContext httpContext, string orderId);

        Task<List<UserOrderResponse>> GetUserOrdersAsync(HttpContext httpContext);

        Task<PaginationResult<UserOrderResponse>> GetAllOrdersAsync(HttpContext httpContext, string? status, int page, int pageSize);

        Task<OrderDetailCustomResponse> GetOrderDetailCustomAsync(HttpContext httpContext, string orderId);

        Task<List<OrderStatusHistoryItemResponse>> GetOrderStatusHistoryAsync(HttpContext httpContext, string orderId);

        Task<CartResponse> UpdateCartItemAsync(HttpContext httpContext, UpdateCartItemRequest request);

        Task<Guid> GetCustomerIdByOrderAsync(string orderId);

        Task<Guid> GetCustomerIdByAssignmentAsync(Guid assignmentId);

        Task<string> GetOrderIdByAssignmentAsync(Guid assignmentId);

        Task<CalculateShippingFeeResponse> CalculateShippingFeeAsync(CalculateShippingFeeRequest request);

        /// <summary>Người dùng xác nhận đã nhận hàng thành công.</summary>
        Task<int> CompleteOrderAsync(HttpContext httpContext, string orderId);

        Task AddToCartNoTransactionAsync(Guid userId, AddToCartRequest request);

        // --------------------- CUSTOMER STAFF ------------------
        Task<CartResponse> GetCartAsync(Guid userId);

        Task<CartResponse> UpdateCartItemAsync(Guid userId, UpdateCartItemRequest request);
    }
}
