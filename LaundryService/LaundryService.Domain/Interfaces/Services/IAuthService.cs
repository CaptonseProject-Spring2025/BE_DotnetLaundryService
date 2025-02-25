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
        Task<LoginResponse> RegisterAsync(RegisterRequest request, string otpToken);

        Task<LoginResponse> LoginAsync(LoginRequest request);

        Task<String> RefreshTokenAsync(Guid userId, string refreshToken);

        Task LogoutAsync(Guid userId);

        Task<User> GetUserByPhoneNumberAsync(string phoneNumber);
    }
}
