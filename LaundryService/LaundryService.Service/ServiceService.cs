using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace LaundryService.Service
{
    public class ServiceService : IServiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHostingEnvironment _webHostEnvironment;

        public ServiceService(IUnitOfWork unitOfWork, IHostingEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IEnumerable<Servicecategory>> GetAllAsync()
        {
            return await _unitOfWork.Repository<Servicecategory>().GetAllAsync();
        }

        public async Task<Servicecategory> GetByIdAsync(Guid id)
        {
            var category = await _unitOfWork.Repository<Servicecategory>().GetAsync(c => c.Categoryid == id);
            if (category == null)
            {
                throw new KeyNotFoundException("Service category not found.");
            }
            return category;
        }

        public async Task<Servicecategory> CreateServiceCategoryAsync(CreateServiceCategoryRequest request)
        {
            // Kiểm tra trùng tên danh mục
            var existingCategory = await _unitOfWork.Repository<Servicecategory>().GetAsync(c => c.Name == request.Name);

            if (existingCategory != null)
            {
                throw new ApplicationException("Service category name already exists.");
            }

            // Tạo tên file ảnh duy nhất
            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.Icon.FileName)}";
            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/service-categories");

            // Đảm bảo thư mục tồn tại
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var filePath = Path.Combine(uploadPath, uniqueFileName);

            // Lưu file vào thư mục
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.Icon.CopyToAsync(stream);
            }

            // Tạo mới danh mục dịch vụ
            var newCategory = new Servicecategory
            {
                Name = request.Name,
                Icon = $"/uploads/service-categories/{uniqueFileName}"
            };

            // Lưu vào database
            await _unitOfWork.Repository<Servicecategory>().InsertAsync(newCategory);
            await _unitOfWork.SaveChangesAsync();

            return newCategory;
        }

        public async Task<Servicecategory> UpdateServiceCategoryAsync(Guid id, UpdateServiceCategoryRequest request)
        {
            var category = await _unitOfWork.Repository<Servicecategory>().GetAsync(c => c.Categoryid == id);
            if (category == null)
            {
                throw new KeyNotFoundException("Service category not found.");
            }

            // Cập nhật tên (nếu có)
            if (!string.IsNullOrEmpty(request.Name))
            {
                category.Name = request.Name;
            }

            // Nếu có ảnh mới, lưu ảnh mới và xóa ảnh cũ
            if (request.Icon != null)
            {
                var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.Icon.FileName)}";
                var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/service-categories");
                Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.Icon.CopyToAsync(stream);
                }

                // Xóa ảnh cũ
                if (!string.IsNullOrEmpty(category.Icon))
                {
                    var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, category.Icon.TrimStart('/'));
                    if (File.Exists(oldFilePath))
                    {
                        File.Delete(oldFilePath);
                    }
                }

                category.Icon = $"/uploads/service-categories/{uniqueFileName}";
            }

            await _unitOfWork.Repository<Servicecategory>().UpdateAsync(category);
            await _unitOfWork.SaveChangesAsync();
            return category;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var category = await _unitOfWork.Repository<Servicecategory>().GetAsync(c => c.Categoryid == id);
            if (category == null)
            {
                throw new KeyNotFoundException("Service category not found.");
            }

            // Kiểm tra nếu có SubService liên quan thì không cho phép xóa
            var subServiceCount = _unitOfWork.Repository<Subservice>().GetAll().Count(s => s.Categoryid == id);
            if (subServiceCount > 0)
            {
                throw new ApplicationException("Cannot delete service category because it has related sub-services.");
            }

            // Xóa ảnh
            if (!string.IsNullOrEmpty(category.Icon))
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, category.Icon.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            await _unitOfWork.Repository<Servicecategory>().DeleteAsync(category);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
