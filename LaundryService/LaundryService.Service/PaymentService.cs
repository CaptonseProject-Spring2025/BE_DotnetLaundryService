using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Dto.Requests;
using Microsoft.EntityFrameworkCore;

namespace LaundryService.Service
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;

        public PaymentService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }

        public async Task<PaymentMethodResponse> CreatePaymentMethodAsync(CreatePaymentMethodRequest request)
        {
            // 1) Kiểm tra trùng tên
            var repo = _unitOfWork.Repository<Paymentmethod>();
            var existing = await repo.GetAll()
                .FirstOrDefaultAsync(pm => pm.Name == request.Name);

            if (existing != null)
            {
                throw new ApplicationException($"PaymentMethod '{request.Name}' đã tồn tại.");
            }

            // 2) Tạo entity mới
            var newPaymentMethod = new Paymentmethod
            {
                Paymentmethodid = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Isactive = request.IsActive ?? true,
                Createdat = DateTime.UtcNow
            };

            // 3) Lưu vào DB
            await _unitOfWork.BeginTransaction();
            try
            {
                await repo.InsertAsync(newPaymentMethod, saveChanges: false);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }

            // 4) Map sang DTO trả về
            var response = new PaymentMethodResponse
            {
                PaymentMethodId = newPaymentMethod.Paymentmethodid,
                Name = newPaymentMethod.Name,
                Description = newPaymentMethod.Description,
                IsActive = newPaymentMethod.Isactive ?? true,
                CreatedAt = _util.ConvertToVnTime(newPaymentMethod.Createdat ?? DateTime.UtcNow)
            };

            return response;
        }

        public async Task<List<PaymentMethodResponse>> GetAllPaymentMethodsAsync()
        {
            // 1) Lấy toàn bộ PaymentMethod từ DB
            var repo = _unitOfWork.Repository<Paymentmethod>();
            var allMethods = await repo
                .GetAll()
                .OrderBy(pm => pm.Createdat)
                .ToListAsync();

            // 2) Map sang DTO
            var result = allMethods.Select(pm => new PaymentMethodResponse
            {
                PaymentMethodId = pm.Paymentmethodid,
                Name = pm.Name,
                Description = pm.Description,
                IsActive = pm.Isactive ?? true,
                CreatedAt = _util.ConvertToVnTime(pm.Createdat ?? DateTime.UtcNow)
            }).ToList();

            return result;
        }
    }
}
