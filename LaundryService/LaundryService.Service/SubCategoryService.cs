using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;

namespace LaundryService.Service
{
    public class SubCategoryService : ISubCategoryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SubCategoryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<SubCategoryResponse>> GetAllByCategoryIdAsync(Guid categoryId)
        {
            var subcategories = _unitOfWork.Repository<Subservice>()
                .GetAll()
                .Where(s => s.Categoryid == categoryId)
                .Select(s => new SubCategoryResponse
                {
                    SubCategoryId = s.Subserviceid,
                    Name = s.Name
                })
                .ToList();

            return await Task.FromResult(subcategories);
        }

        public async Task<SubCategoryResponse> CreateSubCategoryAsync(CreateSubCategoryRequest request)
        {
            // Kiểm tra danh mục cha có tồn tại không
            var categoryExists = await _unitOfWork.Repository<Servicecategory>().GetAsync(c => c.Categoryid == request.CategoryId);
            if (categoryExists == null)
            {
                throw new ArgumentException("Category not found.");
            }

            // Kiểm tra trùng tên trong cùng danh mục cha
            var existingSubCategory = _unitOfWork.Repository<Subservice>()
                .GetAll()
                .FirstOrDefault(s => s.Categoryid == request.CategoryId && s.Name == request.Name);

            if (existingSubCategory != null)
            {
                throw new ArgumentException("Subcategory name already exists in this category.");
            }

            // Tạo mới SubCategory
            var newSubCategory = new Subservice
            {
                Subserviceid = Guid.NewGuid(),
                Categoryid = request.CategoryId,
                Name = request.Name
            };

            await _unitOfWork.Repository<Subservice>().InsertAsync(newSubCategory);
            await _unitOfWork.SaveChangesAsync();

            return new SubCategoryResponse
            {
                SubCategoryId = newSubCategory.Subserviceid,
                Name = newSubCategory.Name
            };
        }

        public async Task<SubCategoryResponse> UpdateSubCategoryAsync(Guid id, UpdateSubCategoryRequest request)
        {
            var subCategory = await _unitOfWork.Repository<Subservice>().GetAsync(s => s.Subserviceid == id);
            if (subCategory == null)
            {
                throw new ArgumentException("Subcategory not found.");
            }

            // Nếu có tên mới, kiểm tra trùng lặp trong cùng Category
            if (!string.IsNullOrEmpty(request.Name) && request.Name != subCategory.Name)
            {
                var isDuplicate = _unitOfWork.Repository<Subservice>()
                    .GetAll()
                    .Any(s => s.Categoryid == subCategory.Categoryid && s.Name == request.Name);

                if (isDuplicate)
                {
                    throw new ArgumentException("A subcategory with this name already exists in the same category.");
                }

                subCategory.Name = request.Name;
            }

            await _unitOfWork.Repository<Subservice>().UpdateAsync(subCategory);
            await _unitOfWork.SaveChangesAsync();

            return new SubCategoryResponse
            {
                SubCategoryId = subCategory.Subserviceid,
                Name = subCategory.Name
            };
        }

        public async Task<bool> DeleteSubCategoryAsync(Guid id)
        {
            var subCategory = await _unitOfWork.Repository<Subservice>().GetAsync(s => s.Subserviceid == id);
            if (subCategory == null)
            {
                throw new ArgumentException("Subcategory not found.");
            }

            // Kiểm tra nếu có ràng buộc dữ liệu với `Servicedetail`
            var hasRelatedServices = _unitOfWork.Repository<Servicedetail>().GetAll().Any(s => s.Subserviceid == id);
            if (hasRelatedServices)
            {
                throw new InvalidOperationException("Cannot delete subcategory because it has related services.");
            }

            await _unitOfWork.Repository<Subservice>().DeleteAsync(subCategory);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
