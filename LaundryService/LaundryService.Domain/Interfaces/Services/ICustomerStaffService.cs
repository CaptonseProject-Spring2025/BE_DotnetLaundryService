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
    public interface ICustomerStaffService
    {
        Task<PaginationResult<UserOrderResponse>> GetPendingOrdersForStaffAsync(HttpContext httpContext, int page, int pageSize);

        Task<Guid> ProcessOrderAsync(HttpContext httpContext, string orderId);

        Task ConfirmOrderAsync(HttpContext httpContext, string orderId, string notes);

        Task<PaginationResult<InCartOrderAdminResponse>> GetInCartOrdersPagedAsync(HttpContext httpContext, int page, int pageSize);

        Task CancelOrderAsync(HttpContext httpContext, Guid assignmentId, string notes);

        Task CancelProcessingAsync(HttpContext httpContext, Guid assignmentId, string note);

        Task StaffAddToCartAsync(Guid userId, AddToCartRequest request);

        Task<string> CusStaffPlaceOrderAsync(HttpContext httpContext, Guid userId, CusStaffPlaceOrderRequest request);
    }
}
