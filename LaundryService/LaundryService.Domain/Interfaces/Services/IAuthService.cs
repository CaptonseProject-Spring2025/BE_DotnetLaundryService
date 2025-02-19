using LaundryService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Dto.Requests;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(RegisterRequest request);
    }
}
