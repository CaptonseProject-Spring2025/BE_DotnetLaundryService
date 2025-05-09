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
using LaundryService.Dto.Responses;

namespace LaundryService.Service
{
    public class ServiceService : IServiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly IUtil _util;

        public ServiceService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _util = util;
        }

        public async Task<IEnumerable<object>> GetAllAsync()
        {
            var categories = await _unitOfWork.Repository<Servicecategory>().GetAllAsync();
            return categories.Select(c => new
            {
                CategoryId = c.Categoryid,
                Name = c.Name,
                Icon = c.Icon,
                CreatedAt = _util.ConvertToVnTime(c.Createdat ?? DateTime.UtcNow),
            }).ToList();
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
            // Check for duplicate category name
            var existingCategory = await _unitOfWork.Repository<Servicecategory>().GetAsync(c => c.Name == request.Name);

            if (existingCategory != null)
            {
                throw new ApplicationException("Service category name already exists.");
            }

            // Upload icon to B2
            var iconUrl = await _fileStorageService.UploadFileAsync(request.Icon, "system-image");

            // Create new service category
            var newCategory = new Servicecategory
            {
                Name = request.Name,
                Icon = iconUrl
            };

            // Save to database
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

            // Update name if provided
            if (!string.IsNullOrEmpty(request.Name))
            {
                category.Name = request.Name;
            }

            // Update icon if provided
            if (request.Icon != null)
            {
                // Delete old icon from B2
                if (!string.IsNullOrEmpty(category.Icon))
                {
                    await _fileStorageService.DeleteFileAsync(category.Icon);
                }

                // Upload new icon to B2
                category.Icon = await _fileStorageService.UploadFileAsync(request.Icon, "system-image");
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

            // Check if there are related sub-services
            var subServiceCount = _unitOfWork.Repository<Subservice>().GetAll().Count(s => s.Categoryid == id);
            if (subServiceCount > 0)
            {
                throw new ApplicationException("Cannot delete service category because it has related sub-services.");
            }

            // Delete icon from B2
            if (!string.IsNullOrEmpty(category.Icon))
            {
                await _fileStorageService.DeleteFileAsync(category.Icon);
            }

            await _unitOfWork.Repository<Servicecategory>().DeleteAsync(category);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<CategoryDetailResponse> GetCategoryDetailsAsync(Guid id)
        {
            var category = await _unitOfWork.Repository<Servicecategory>().GetAsync(c => c.Categoryid == id);
            if (category == null)
            {
                throw new KeyNotFoundException("Service category not found.");
            }

            // Lấy danh sách subcategories
            var subCategories = _unitOfWork.Repository<Subservice>()
                .GetAll()
                .Where(s => s.Categoryid == id)
                .Select(s => new SubCategory
                {
                    SubCategoryId = s.Subserviceid,
                    Name = s.Name,
                    ServiceDetails = _unitOfWork.Repository<Servicedetail>()
                        .GetAll()
                        .Where(sd => sd.Subserviceid == s.Subserviceid)
                        .Select(sd => new ServiceDetailResponse
                        {
                            ServiceId = sd.Serviceid,
                            Name = sd.Name,
                            Description = sd.Description,
                            Price = sd.Price,
                            ImageUrl = sd.Image,
                            CreatedAt = _util.ConvertToVnTime(sd.Createdat ?? DateTime.UtcNow),
                        })
                        .ToList()
                })
                .ToList();

            return new CategoryDetailResponse
            {
                CategoryId = category.Categoryid,
                Name = category.Name,
                Icon = category.Icon,
                SubCategories = subCategories
            };
        }

        /* ========== CASCADE DELETE ========== */
        public async Task<bool> DeleteCategoryCascadeAsync(Guid categoryId)
        {
            // 1) Lấy ServiceCategory
            var categoryRepo = _unitOfWork.Repository<Servicecategory>();
            var category = await categoryRepo.GetAsync(c => c.Categoryid == categoryId);
            if (category == null) throw new KeyNotFoundException("Service category not found.");

            // 2) Bắt đầu transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                /* ----------------------------------------------------------------
                 * 2.1) XÓA ServiceDetail  ➜  ServiceExtraMapping  ➜  Image
                 * ----------------------------------------------------------------*/
                var subServiceRepo = _unitOfWork.Repository<Subservice>();
                var detailRepo = _unitOfWork.Repository<Servicedetail>();
                var mappingRepo = _unitOfWork.Repository<Serviceextramapping>();

                // Lấy tất cả SubService thuộc category
                var subServices = subServiceRepo.GetAll()
                                                .Where(ss => ss.Categoryid == categoryId)
                                                .ToList();

                foreach (var sub in subServices)
                {
                    // Lấy ServiceDetail của SubService hiện tại
                    var details = detailRepo.GetAll()
                                            .Where(d => d.Subserviceid == sub.Subserviceid)
                                            .ToList();

                    foreach (var d in details)
                    {
                        // 2.1.1) Xóa ServiceExtraMapping
                        var mappings = mappingRepo.GetAll()
                                                  .Where(m => m.Serviceid == d.Serviceid)
                                                  .ToList();
                        if (mappings.Any())
                            await mappingRepo.DeleteRangeAsync(mappings);

                        // 2.1.2) Xóa ảnh ServiceDetail (nếu có)
                        if (!string.IsNullOrEmpty(d.Image))
                            await _fileStorageService.DeleteFileAsync(d.Image);

                        // 2.1.3) Xóa ServiceDetail
                        await detailRepo.DeleteAsync(d);
                    }
                }

                /* ---------------------------------------------------
                 * 2.2) XÓA SubService sau khi đã rỗng ServiceDetail
                 * ---------------------------------------------------*/
                if (subServices.Any())
                    await _unitOfWork.Repository<Subservice>().DeleteRangeAsync(subServices);

                /* -----------------------------------------------
                 * 2.3) XÓA ảnh Icon & bản ghi ServiceCategory
                 * -----------------------------------------------*/
                if (!string.IsNullOrEmpty(category.Icon))
                    await _fileStorageService.DeleteFileAsync(category.Icon);

                await categoryRepo.DeleteAsync(category);

                /* -------------------------------------------
                 * 2.4) Lưu DB & Commit Transaction
                 * -------------------------------------------*/
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;      // propagate cho controller xử lý
            }
        }
    }
}
