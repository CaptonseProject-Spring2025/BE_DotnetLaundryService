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
    public class ExtraService : IExtraService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;

        public ExtraService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
        }

        public async Task<ExtraResponse> GetExtraByIdAsync(Guid extraId)
        {
            var extra = await _unitOfWork.Repository<Extra>().GetAsync(e => e.Extraid == extraId);
            if (extra == null)
            {
                throw new KeyNotFoundException("Extra not found.");
            }

            return new ExtraResponse
            {
                ExtraId = extra.Extraid,
                Name = extra.Name,
                Description = extra.Description,
                Price = extra.Price,
                ImageUrl = extra.Image,
                CreatedAt = extra.Createdat
            };
        }

        public async Task<ExtraResponse> CreateExtraAsync(CreateExtraRequest request)
        {
            // Kiểm tra ExtraCategory có tồn tại không
            var extraCategory = await _unitOfWork.Repository<Extracategory>()
                .GetAsync(ec => ec.Extracategoryid == request.ExtraCategoryId);

            if (extraCategory == null)
            {
                throw new ArgumentException("Extra category not found.");
            }

            // Kiểm tra trùng tên trong cùng một ExtraCategory
            var existingExtra = _unitOfWork.Repository<Extra>()
                .GetAll()
                .FirstOrDefault(e => e.Extracategoryid == request.ExtraCategoryId && e.Name == request.Name);

            if (existingExtra != null)
            {
                throw new ArgumentException("An extra with this name already exists in the same category.");
            }

            // Upload hình ảnh lên Backblaze B2 nếu có
            string imageUrl = null;
            if (request.Image != null)
            {
                imageUrl = await _fileStorageService.UploadFileAsync(request.Image, "extras");
            }

            // Tạo mới Extra
            var newExtra = new Extra
            {
                Extracategoryid = request.ExtraCategoryId,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Image = imageUrl,
                Createdat = DateTime.Now
            };

            await _unitOfWork.Repository<Extra>().InsertAsync(newExtra);
            await _unitOfWork.SaveChangesAsync();

            return new ExtraResponse
            {
                ExtraId = newExtra.Extraid,
                Name = newExtra.Name,
                Description = newExtra.Description,
                Price = newExtra.Price,
                ImageUrl = newExtra.Image,
                CreatedAt = newExtra.Createdat
            };
        }

        public async Task<ExtraResponse> UpdateExtraAsync(UpdateExtraRequest request)
        {
            var extra = await _unitOfWork.Repository<Extra>().GetAsync(e => e.Extraid == request.ExtraId);
            if (extra == null)
            {
                throw new ArgumentException("Extra not found.");
            }

            // Cập nhật Name nếu có
            if (!string.IsNullOrEmpty(request.Name))
            {
                var isDuplicate = _unitOfWork.Repository<Extra>()
                    .GetAll()
                    .Any(e => e.Extracategoryid == extra.Extracategoryid && e.Name == request.Name && e.Extraid != request.ExtraId);

                if (isDuplicate)
                {
                    throw new ArgumentException("An extra with this name already exists in the same category.");
                }

                extra.Name = request.Name;
            }

            // Cập nhật Description nếu có
            if (!string.IsNullOrEmpty(request.Description))
            {
                extra.Description = request.Description;
            }

            // Cập nhật Price nếu có
            if (request.Price.HasValue)
            {
                extra.Price = request.Price.Value;
            }

            // Cập nhật Image nếu có
            if (request.Image != null)
            {
                // Xóa ảnh cũ trên B2 Storage nếu có
                if (!string.IsNullOrEmpty(extra.Image))
                {
                    await _fileStorageService.DeleteFileAsync(extra.Image);
                }

                // Upload ảnh mới
                extra.Image = await _fileStorageService.UploadFileAsync(request.Image, "extras");
            }

            await _unitOfWork.Repository<Extra>().UpdateAsync(extra);
            await _unitOfWork.SaveChangesAsync();

            return new ExtraResponse
            {
                ExtraId = extra.Extraid,
                Name = extra.Name,
                Description = extra.Description,
                Price = extra.Price,
                ImageUrl = extra.Image,
                CreatedAt = extra.Createdat
            };
        }

        public async Task<bool> DeleteExtraAsync(Guid extraId)
        {
            var extra = await _unitOfWork.Repository<Extra>().GetAsync(e => e.Extraid == extraId);
            if (extra == null)
            {
                throw new ArgumentException("Extra not found.");
            }

            // Kiểm tra nếu Extra có ràng buộc với OrderExtra
            var hasOrderExtras = _unitOfWork.Repository<Orderextra>()
                .GetAll()
                .Any(o => o.Extraid == extraId);

            if (hasOrderExtras)
            {
                throw new InvalidOperationException("Cannot delete extra because it has related orders.");
            }

            // Kiểm tra nếu Extra có ràng buộc với ServiceExtraMapping
            var hasExtraMappings = _unitOfWork.Repository<Serviceextramapping>()
                .GetAll()
                .Any(e => e.Extraid == extraId);

            if (hasExtraMappings)
            {
                throw new InvalidOperationException("Cannot delete extra because it has associated service mappings.");
            }

            // Xóa ảnh trên B2 Storage nếu có
            if (!string.IsNullOrEmpty(extra.Image))
            {
                await _fileStorageService.DeleteFileAsync(extra.Image);
            }

            // Xóa Extra khỏi database
            await _unitOfWork.Repository<Extra>().DeleteAsync(extra);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }
}
