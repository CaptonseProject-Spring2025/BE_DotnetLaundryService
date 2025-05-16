using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class AreaService : IAreaService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;

        public AreaService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }

        public async Task AddOrReplaceAreasAsync(AddAreasRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AreaType))
                throw new ArgumentException("AreaType is required.");

            if (request.Areas is null || request.Areas.Count == 0)
                throw new ArgumentException("Areas list is empty.");

            await _unitOfWork.BeginTransaction();
            try
            {
                /* 1) Xóa các bản ghi Area có AreaType trùng */
                var areaRepo = _unitOfWork.Repository<Area>();
                var oldAreas = areaRepo.GetAll()
                                        .Where(a => a.Areatype == request.AreaType)
                                        .ToList();

                if (oldAreas.Any())
                    await areaRepo.DeleteRangeAsync(oldAreas, saveChanges: false);

                /* 2) Thêm mới từng AreaItem */
                foreach (var item in request.Areas)
                {
                    if (string.IsNullOrWhiteSpace(item.Name))
                        throw new ArgumentException("Area name cannot be empty.");

                    var newArea = new Area
                    {
                        Areaid = Guid.NewGuid(),
                        Name = item.Name.Trim(),
                        Districts = item.Districts ?? new List<string>(),
                        Areatype = request.AreaType.Trim()
                    };

                    await areaRepo.InsertAsync(newArea, saveChanges: false);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
