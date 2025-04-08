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
    }
}
