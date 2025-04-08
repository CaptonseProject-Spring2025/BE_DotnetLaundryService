using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LaundryService.Service
{
    public class PhotoService : IPhotoService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;

        public PhotoService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
        }

        public async Task<List<string>> GetPhotoUrlsByStatusHistoryIdAsync(Guid statusHistoryId)
        {
            // 1) Kiểm tra xem statusHistory có tồn tại không (nếu muốn báo lỗi 404)
            var statusHistory = await _unitOfWork.Repository<Orderstatushistory>()
                .GetAll()
                .FirstOrDefaultAsync(x => x.Statushistoryid == statusHistoryId);
            if (statusHistory == null)
            {
                // Tùy xử lý. Hoặc throw KeyNotFoundException => 404
                throw new KeyNotFoundException("Status history not found.");
            }

            // 2) Lấy tất cả Orderphoto cho statusHistoryId
            var photos = await _unitOfWork.Repository<Orderphoto>()
                .GetAll()
                .Where(p => p.Statushistoryid == statusHistoryId)
                .Select(p => p.Photourl)
                .ToListAsync();

            return photos;
        }

        public async Task DeletePhotoByUrlAsync(string photoUrl)
        {
            if (string.IsNullOrWhiteSpace(photoUrl))
                throw new ArgumentException("PhotoUrl is required.");

            // Mở transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                // 1) Tìm record trong bảng Orderphoto
                var photo = _unitOfWork.Repository<Orderphoto>()
                    .GetAll()
                    .FirstOrDefault(x => x.Photourl == photoUrl);

                if (photo == null)
                    throw new KeyNotFoundException("Photo record not found for the specified PhotoUrl.");

                // 2) Gọi B2StorageService xóa file
                //    (nếu file không có trên B2 => nó log lỗi, ta vẫn tiếp tục xóa record DB)
                await _fileStorageService.DeleteFileAsync(photoUrl);

                // 3) Xóa record Photo trong DB
                await _unitOfWork.Repository<Orderphoto>().DeleteAsync(photo, saveChanges: false);

                // 4) Lưu, commit transaction
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                // Nếu có lỗi => rollback
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
