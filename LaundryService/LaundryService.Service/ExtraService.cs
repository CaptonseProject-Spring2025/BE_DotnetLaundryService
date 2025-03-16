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
    }
}
