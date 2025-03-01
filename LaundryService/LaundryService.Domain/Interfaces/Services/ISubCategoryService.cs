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
    public interface ISubCategoryService
    {
        Task<IEnumerable<SubCategoryResponse>> GetAllByCategoryIdAsync(Guid categoryId);
        Task<SubCategoryResponse> CreateSubCategoryAsync(CreateSubCategoryRequest request);
        Task<SubCategoryResponse> UpdateSubCategoryAsync(Guid id, UpdateSubCategoryRequest request);
        Task<bool> DeleteSubCategoryAsync(Guid id);
    }
}
