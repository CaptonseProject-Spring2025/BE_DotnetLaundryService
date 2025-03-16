using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IExtraCategoryService
    {
        Task<List<ExtraCategoryDetailResponse>> GetAllExtraCategoriesAsync();

        Task<ExtraCategoryResponse> CreateExtraCategoryAsync(CreateExtraCategoryRequest request);

        Task<bool> DeleteExtraCategoryAsync(Guid id);
    }
}
