using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class ServiceDetailService : IServiceDetailService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;

        public ServiceDetailService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
        }

        public async Task<ServiceDetailResponse> CreateServiceDetailAsync(CreateServiceDetailRequest request)
        {
            // Kiểm tra Subservice có tồn tại không
            var subserviceExists = await _unitOfWork.Repository<Subservice>().GetAsync(s => s.Subserviceid == request.SubCategoryId);
            if (subserviceExists == null)
            {
                throw new ArgumentException("SubCategory not found.");
            }

            // Kiểm tra trùng tên trong cùng một Subservice
            var existingService = _unitOfWork.Repository<Servicedetail>()
                .GetAll()
                .FirstOrDefault(s => s.Subserviceid == request.SubCategoryId && s.Name == request.Name);

            if (existingService != null)
            {
                throw new ArgumentException("A service detail with this name already exists in the same subservice.");
            }

            // Upload hình ảnh lên Backblaze B2
            string imageUrl = null;
            if (request.Image != null)
            {
                imageUrl = await _fileStorageService.UploadFileAsync(request.Image, "service-details");
            }

            // Tạo mới ServiceDetail
            var newServiceDetail = new Servicedetail
            {
                Subserviceid = request.SubCategoryId,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Image = imageUrl
            };

            await _unitOfWork.Repository<Servicedetail>().InsertAsync(newServiceDetail);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceDetailResponse
            {
                ServiceId = newServiceDetail.Serviceid,
                Name = newServiceDetail.Name,
                Description = newServiceDetail.Description,
                Price = newServiceDetail.Price,
                ImageUrl = newServiceDetail.Image,
                CreatedAt = newServiceDetail.Createdat
            };
        }

        public async Task<ServiceDetailResponse> UpdateServiceDetailAsync(UpdateServiceDetailRequest request)
        {
            var serviceDetail = await _unitOfWork.Repository<Servicedetail>()
                .GetAsync(s => s.Serviceid == request.ServiceId);

            if (serviceDetail == null)
            {
                throw new ArgumentException("Service detail not found.");
            }

            // Kiểm tra Name có trùng trong cùng Subservice không
            if (!string.IsNullOrEmpty(request.Name) && request.Name != serviceDetail.Name)
            {
                var isDuplicate = _unitOfWork.Repository<Servicedetail>()
                    .GetAll()
                    .Any(s => s.Subserviceid == serviceDetail.Subserviceid && s.Name == request.Name);

                if (isDuplicate)
                {
                    throw new ArgumentException("A service detail with this name already exists in the same subservice.");
                }

                serviceDetail.Name = request.Name;
            }

            // Cập nhật Description nếu có
            if (!string.IsNullOrEmpty(request.Description))
            {
                serviceDetail.Description = request.Description;
            }

            // Cập nhật Price nếu có
            if (request.Price.HasValue)
            {
                serviceDetail.Price = request.Price.Value;
            }

            // Xử lý cập nhật Image
            if (request.Image != null)
            {
                // Xóa ảnh cũ trên B2
                if (!string.IsNullOrEmpty(serviceDetail.Image))
                {
                    await _fileStorageService.DeleteFileAsync(serviceDetail.Image);
                }

                // Upload ảnh mới
                serviceDetail.Image = await _fileStorageService.UploadFileAsync(request.Image, "service-details");
            }

            await _unitOfWork.Repository<Servicedetail>().UpdateAsync(serviceDetail);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceDetailResponse
            {
                ServiceId = serviceDetail.Serviceid,
                Name = serviceDetail.Name,
                Description = serviceDetail.Description,
                Price = serviceDetail.Price,
                ImageUrl = serviceDetail.Image
            };
        }

        public async Task<bool> DeleteServiceDetailAsync(Guid serviceId)
        {
            var serviceDetail = await _unitOfWork.Repository<Servicedetail>()
                .GetAsync(s => s.Serviceid == serviceId);

            if (serviceDetail == null)
            {
                throw new ArgumentException("Service detail not found.");
            }

            // Kiểm tra nếu có ràng buộc với OrderItem
            var hasOrderItems = _unitOfWork.Repository<Orderitem>()
                .GetAll()
                .Any(o => o.Serviceid == serviceId);

            if (hasOrderItems)
            {
                throw new InvalidOperationException("Cannot delete service detail because it has related orders.");
            }

            // Kiểm tra nếu có ràng buộc với Ratings
            var hasRatings = _unitOfWork.Repository<Rating>()
                .GetAll()
                .Any(r => r.Serviceid == serviceId);

            if (hasRatings)
            {
                throw new InvalidOperationException("Cannot delete service detail because it has associated ratings.");
            }

            // Kiểm tra nếu có ràng buộc với ServiceExtraMapping
            var hasExtraMappings = _unitOfWork.Repository<Serviceextramapping>()
                .GetAll()
                .Any(e => e.Serviceid == serviceId);

            if (hasExtraMappings)
            {
                throw new InvalidOperationException("Cannot delete service detail because it has associated extras.");
            }

            // Xóa ảnh trên B2 Storage nếu có
            if (!string.IsNullOrEmpty(serviceDetail.Image))
            {
                await _fileStorageService.DeleteFileAsync(serviceDetail.Image);
            }

            // Xóa ServiceDetail khỏi database
            await _unitOfWork.Repository<Servicedetail>().DeleteAsync(serviceDetail);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }
}
