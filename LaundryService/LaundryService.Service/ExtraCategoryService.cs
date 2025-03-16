using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class ExtraCategoryService : IExtraCategoryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ExtraCategoryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<ExtraCategoryDetailResponse>> GetAllExtraCategoriesAsync()
        {
            var extraCategories = await _unitOfWork.Repository<Extracategory>().GetAllAsync();

            var result = extraCategories.Select(category => new ExtraCategoryDetailResponse
            {
                ExtraCategoryId = category.Extracategoryid,
                Name = category.Name,
                CreatedAt = category.Createdat,
                Extras = _unitOfWork.Repository<Extra>()
                    .GetAll()
                    .Where(e => e.Extracategoryid == category.Extracategoryid)
                    .Select(extra => new ExtraResponse
                    {
                        ExtraId = extra.Extraid,
                        Name = extra.Name,
                        Description = extra.Description,
                        Price = extra.Price,
                        ImageUrl = extra.Image,
                        CreatedAt = extra.Createdat
                    })
                    .ToList()
            }).ToList();

            return result;
        }

        public async Task<ExtraCategoryResponse> CreateExtraCategoryAsync(CreateExtraCategoryRequest request)
        {
            // Kiểm tra trùng tên ExtraCategory
            var existingCategory = await _unitOfWork.Repository<Extracategory>()
                .GetAsync(e => e.Name == request.Name);
            if (existingCategory != null)
            {
                throw new ApplicationException("Extra category name already exists.");
            }

            // Tạo mới ExtraCategory
            var newCategory = new Extracategory
            {
                Name = request.Name,
                Createdat = DateTime.Now
            };

            // Thêm vào database
            await _unitOfWork.Repository<Extracategory>().InsertAsync(newCategory);
            await _unitOfWork.SaveChangesAsync();

            // Trả về dữ liệu response
            return new ExtraCategoryResponse
            {
                ExtraCategoryId = newCategory.Extracategoryid,
                Name = newCategory.Name,
                CreatedAt = newCategory.Createdat
            };
        }

        public async Task<bool> DeleteExtraCategoryAsync(Guid id)
        {
            // Tìm ExtraCategory trong database
            var category = await _unitOfWork.Repository<Extracategory>().GetAsync(e => e.Extracategoryid == id);
            if (category == null)
            {
                throw new KeyNotFoundException("Extra category not found.");
            }

            // Kiểm tra xem ExtraCategory có ràng buộc với Extra không
            var hasExtras = _unitOfWork.Repository<Extra>().GetAll().Any(e => e.Extracategoryid == id);
            if (hasExtras)
            {
                throw new ApplicationException("Cannot delete extra category because it has related extras.");
            }

            // Xóa danh mục
            await _unitOfWork.Repository<Extracategory>().DeleteAsync(category);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
