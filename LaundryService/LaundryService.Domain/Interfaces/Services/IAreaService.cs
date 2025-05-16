using LaundryService.Dto.Requests;
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
    }
}
