using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IUserService
    {
        Task<UserDetailResponse> GetUserByIdAsync(Guid userId);
    }
}
