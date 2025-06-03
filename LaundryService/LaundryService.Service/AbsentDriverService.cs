using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class AbsentDriverService : IAbsentDriverService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AbsentDriverService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        private static DateTime ToUtc(DateOnly d, TimeSpan t)
            => DateTime.SpecifyKind(
                   d.ToDateTime(TimeOnly.FromTimeSpan(t)),
                   DateTimeKind.Utc);

        private async Task ValidateOverlapAsync(Guid driverId, DateTime startUtc, DateTime endUtc, Guid? ignoreId = null)
        {
            bool overlap = await _unitOfWork.Repository<Absentdriver>()
                .GetAll()
                .AnyAsync(a =>
                    a.Driverid == driverId &&
                    (ignoreId == null || a.Absentid != ignoreId) &&
                    a.Absentfrom < endUtc &&
                    a.Absentto > startUtc);

            if (overlap) throw new InvalidOperationException("Khoảng thời gian vắng trùng với lịch đã có.");
        }

        public async Task<AbsentDriverResponse> AddAbsentAsync(AbsentDriverCreateRequest req)
        {
            if (req.To <= req.From) throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");

            var startUtc = ToUtc(req.Date, req.From);
            var endUtc = ToUtc(req.Date, req.To);

            bool hasOpenJobs = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .AnyAsync(o =>
                    o.Assignedto == req.DriverId &&
                   (o.Status == AssignStatusEnum.ASSIGNED_PICKUP.ToString() ||
                    o.Status == AssignStatusEnum.ASSIGNED_DELIVERY.ToString()));

            if (hasOpenJobs)
                throw new InvalidOperationException("Tài xế đang có nhiệm vụ PICKUP / DELIVERY, không thể tạo lịch vắng.");

            await ValidateOverlapAsync(req.DriverId, startUtc, endUtc);

            var absent = new Absentdriver
            {
                Absentid = Guid.NewGuid(),
                Driverid = req.DriverId,
                Dateabsent = req.Date,
                Absentfrom = startUtc,
                Absentto = endUtc,
                Datecreated = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Absentdriver>().InsertAsync(absent);
            await _unitOfWork.SaveChangesAsync();

            return Map(absent);
        }

        public async Task<AbsentDriverResponse> UpdateAbsentAsync(Guid absentId, AbsentDriverUpdateRequest req)
        {
            if (req.To <= req.From) throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");

            var repo = _unitOfWork.Repository<Absentdriver>();
            var absent = await repo.GetAsync(a => a.Absentid == absentId)
                         ?? throw new KeyNotFoundException("Không tìm thấy bản ghi.");

            var startUtc = ToUtc(req.Date, req.From);
            var endUtc = ToUtc(req.Date, req.To);

            await ValidateOverlapAsync(absent.Driverid, startUtc, endUtc, absentId);

            absent.Dateabsent = req.Date;
            absent.Absentfrom = startUtc;
            absent.Absentto = endUtc;

            await repo.UpdateAsync(absent);
            await _unitOfWork.SaveChangesAsync();
            return Map(absent);
        }

        public async Task DeleteAbsentAsync(Guid absentId)
        {
            var repo = _unitOfWork.Repository<Absentdriver>();
            var abs = await repo.GetAsync(a => a.Absentid == absentId)
                      ?? throw new KeyNotFoundException("Không tìm thấy bản ghi.");

            await repo.DeleteAsync(abs);
            await _unitOfWork.SaveChangesAsync();
        }

        private static AbsentDriverResponse Map(Absentdriver a) => new()
        {
            AbsentId = a.Absentid,
            DriverId = a.Driverid,
            Date = a.Dateabsent,
            From = a.Absentfrom.TimeOfDay,
            To = a.Absentto.TimeOfDay,
            CreatedAtUtc = a.Datecreated ?? DateTime.UtcNow
        };

        public async Task<List<AbsentDriverListResponse>> GetAllAbsentsAsync()
        {
            var items = await _unitOfWork.Repository<Absentdriver>()
                .GetAll()
                .Include(a => a.Driver)
                .OrderByDescending(a => a.Datecreated ?? a.Absentfrom)
                .ToListAsync();

            return items.Select(a => new AbsentDriverListResponse
            {
                AbsentId = a.Absentid,
                DriverId = a.Driverid,
                FullName = a.Driver.Fullname,
                PhoneNumber = a.Driver.Phonenumber,
                Date = a.Dateabsent,
                From = a.Absentfrom.TimeOfDay,
                To = a.Absentto.TimeOfDay,
                CreatedAtUtc = a.Datecreated ?? a.Absentfrom
            }).ToList();
        }
    }
}