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
    public interface IAddressService
    {
        Task<AddressResponse> CreateAddressAsync(HttpContext httpContext, CreateAddressRequest request);

        Task<AddressResponse> CreateAddressAsync(Guid userId, CreateAddressRequest request);

        Task<bool> DeleteAddressAsync(HttpContext httpContext, Guid addressId);

        Task<List<AddressResponse>> GetUserAddressesAsync(HttpContext httpContext);

        Task<AddressResponse> GetAddressByIdAsync(HttpContext httpContext, Guid addressId);

        Task<AddressInfoResponse> GetPickupAddressFromAssignmentAsync(HttpContext httpContext, Guid assignmentId);

        Task<AddressInfoResponse> GetDeliveryAddressFromAssignmentAsync(HttpContext httpContext, Guid assignmentId);
    }
}
