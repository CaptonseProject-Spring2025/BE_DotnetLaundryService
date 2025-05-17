using LaundryService.Domain.Entities;
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

            if (!request.AreaType.Trim().ToLower().Equals("shippingfee") && !request.AreaType.Trim().ToLower().Equals("driver"))
                throw new ArgumentException("AreaType must be either 'shippingFee' or 'driver'.");

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

        public async Task<List<AreaItemResponse>> GetAreasByTypeAsync(string areaType)
        {
            if (string.IsNullOrWhiteSpace(areaType))
                throw new ArgumentException("AreaType is required.");

            if (!areaType.Trim().ToLower().Equals("shippingfee") && !areaType.Trim().ToLower().Equals("driver"))
                throw new ArgumentException("AreaType must be either 'shippingFee' or 'driver'.");

            var areas = _unitOfWork.Repository<Area>()
                                   .GetAll()
                                   .Where(a => a.Areatype == areaType.Trim())
                                   .OrderBy(a => a.Name)
                                   .Select(a => new AreaItemResponse
                                   {
                                       AreaId = a.Areaid,
                                       Name = a.Name,
                                       Districts = a.Districts ?? new List<string>()
                                   })
                                   .ToList();

            return await Task.FromResult(areas);   // Linq ToObjects ⇒ không cần EF async
        }

        public async Task UpdateAreaByIdAsync(Guid areaId, string name, List<string> districts)
        {
            if (areaId == Guid.Empty)
                throw new ArgumentException("AreaId is required.");
            var area = await _unitOfWork.Repository<Area>().FindAsync(areaId);
            if (area == null)
                throw new KeyNotFoundException($"Area with ID {areaId} not found.");

            if (!string.IsNullOrWhiteSpace(name))
                area.Name = name.Trim();

            if (districts.Any())
                area.Districts = districts;
            
            await _unitOfWork.Repository<Area>().UpdateAsync(area, saveChanges: false);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAreaByIdAsync(Guid areaId)
        {
            if (areaId == Guid.Empty)
                throw new ArgumentException("AreaId is required.");

            var area = await _unitOfWork.Repository<Area>().FindAsync(areaId);
            if (area == null)
                throw new KeyNotFoundException($"Area with ID {areaId} not found.");

            await _unitOfWork.Repository<Area>().DeleteAsync(area, saveChanges: false);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<Branchaddress> AddBranchAddressAsync(AddBranchAddressRequest request)
        {
            var branchAddress = new Branchaddress
            {
                Brachid = Guid.NewGuid(),
                Addressdetail = request.Addressdetail.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };
            await _unitOfWork.Repository<Branchaddress>().InsertAsync(branchAddress, saveChanges: false);
            await _unitOfWork.SaveChangesAsync();
            return branchAddress;
        }

        public async Task<List<Branchaddress>> GetBranchAddressAsync()
        {
            var branchAddress = await _unitOfWork.Repository<Branchaddress>().GetAllAsync();
            if (branchAddress == null)
                throw new KeyNotFoundException("Branch address not found.");
            return branchAddress.ToList();
        }

        public async Task<Branchaddress> UpdateBranchAddressAsync(Guid brachId, UpdateBranchAddressRequest request)
        {
            var branchAddress = await _unitOfWork.Repository<Branchaddress>().FindAsync(brachId);
            if (branchAddress == null)
                throw new KeyNotFoundException($"Branch address with ID {brachId} not found.");

            if (!string.IsNullOrWhiteSpace(request.Addressdetail))
                branchAddress.Addressdetail = request.Addressdetail.Trim();

            if (request.Latitude.HasValue)
                branchAddress.Latitude = request.Latitude;

            if (request.Longitude.HasValue)
                branchAddress.Longitude = request.Longitude;

            await _unitOfWork.Repository<Branchaddress>().UpdateAsync(branchAddress, saveChanges: false);
            await _unitOfWork.SaveChangesAsync();
            return branchAddress;
        }
    }
}
