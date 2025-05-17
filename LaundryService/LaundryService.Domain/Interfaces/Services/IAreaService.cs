using LaundryService.Domain.Entities;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IAreaService
    {
        /// <summary>
        /// Thêm mới hoặc thay thế toàn bộ Area theo AreaType.
        /// </summary>
        Task AddOrReplaceAreasAsync(AddAreasRequest request);

        /// <summary>
        /// Lấy danh sách Area theo AreaType, sắp xếp theo Name.
        /// </summary>
        Task<List<AreaItemResponse>> GetAreasByTypeAsync(string areaType);

        Task UpdateAreaByIdAsync(Guid areaId, string name, List<string> districts);

        Task DeleteAreaByIdAsync(Guid areaId);

        Task<Branchaddress> AddBranchAddressAsync(AddBranchAddressRequest request);

        Task<List<Branchaddress>> GetBranchAddressAsync();

        Task<Branchaddress> UpdateBranchAddressAsync(Guid brachId, UpdateBranchAddressRequest request);
    }
}
