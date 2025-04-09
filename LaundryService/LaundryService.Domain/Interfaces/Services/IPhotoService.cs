using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IPhotoService
    {
        Task<List<PhotoInfo>> GetPhotoUrlsByStatusHistoryIdAsync(Guid statusHistoryId);

        Task DeletePhotoByUrlAsync(string photoUrl);
    }
}
