using LaundryService.Domain.Entities;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IServiceService
    {
        Task<IEnumerable<object>> GetAllAsync();
        Task<Servicecategory> GetByIdAsync(Guid id);
        Task<Servicecategory> CreateServiceCategoryAsync(CreateServiceCategoryRequest request);
        Task<Servicecategory> UpdateServiceCategoryAsync(Guid id, UpdateServiceCategoryRequest request);
        Task<bool> DeleteAsync(Guid id);

        //Lấy thông tin chi tiết danh mục
        Task<CategoryDetailResponse> GetCategoryDetailsAsync(Guid id);
    }
}
