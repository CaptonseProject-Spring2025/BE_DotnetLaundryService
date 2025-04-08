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

        public PhotoService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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
    }
}
