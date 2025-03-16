using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IServiceDetailService
    {
        Task<ServiceDetailResponse> CreateServiceDetailAsync(CreateServiceDetailRequest request);

        Task<ServiceDetailResponse> UpdateServiceDetailAsync(UpdateServiceDetailRequest request);

        Task<bool> DeleteServiceDetailAsync(Guid serviceId);

        Task<AddExtrasToServiceDetailResponse> AddExtrasToServiceDetailAsync(AddExtrasToServiceDetailRequest request);
    }
}
