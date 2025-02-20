using LaundryService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> RegisterAsync(RegisterRequest request);

        Task<LoginResponse> LoginAsync(LoginRequest request);

        Task<RefreshTokenResponse> RefreshTokenAsync(Guid userId, string refreshToken);

        Task<User> GetUserByPhoneNumberAsync(string phoneNumber);
    }
}
