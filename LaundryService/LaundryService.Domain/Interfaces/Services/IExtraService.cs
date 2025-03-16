using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IExtraService
    {
        Task<ExtraResponse> GetExtraByIdAsync(Guid extraId);

        Task<ExtraResponse> CreateExtraAsync(CreateExtraRequest request);

        Task<bool> DeleteExtraAsync(Guid extraId);

        Task<ExtraResponse> UpdateExtraAsync(UpdateExtraRequest request);
    }
}
