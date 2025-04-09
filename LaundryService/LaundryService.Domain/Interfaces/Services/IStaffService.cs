using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IStaffService
    {
        Task<List<PickedUpOrderResponse>> GetPickedUpOrdersAsync(HttpContext httpContext);

        Task ReceiveOrderForCheckAsync(HttpContext httpContext, string orderId);

        Task<List<PickedUpOrderResponse>> GetCheckingOrdersAsync(HttpContext httpContext);

        Task<CheckingOrderUpdateResponse> UpdateCheckingOrderAsync(HttpContext httpContext, string orderId, string? notes, IFormFileCollection? files);

        Task ConfirmCheckingDoneAsync(HttpContext httpContext, string orderId, string notes);

        // Lấy các đơn ở trạng thái CHECKED để nhân viên kế tiếp xử lý (đem đi giặt).
        Task<List<PickedUpOrderResponse>> GetCheckedOrdersAsync(HttpContext httpContext);

        Task<Guid> ReceiveOrderForWashingAsync(HttpContext httpContext, string orderId, string? notes, IFormFileCollection? files);

        Task<List<PickedUpOrderResponse>> GetWashingOrdersAsync(HttpContext httpContext);

        Task<CheckingOrderUpdateResponse> UpdateWashingOrderAsync(HttpContext httpContext, string orderId, string? notes, IFormFileCollection? files);
    }
}
