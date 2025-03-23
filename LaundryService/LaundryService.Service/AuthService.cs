using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Dto.Responses;
using Microsoft.Extensions.Caching.Memory;

namespace LaundryService.Service
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly IMemoryCache _memoryCache;

        public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService, IMemoryCache memoryCache)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _memoryCache = memoryCache;
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request, string otpToken)
        {
            // Kiểm tra số điện thoại đã tồn tại chưa
            var existingUser = await _unitOfWork.Repository<User>().GetAsync(u => u.Phonenumber == request.PhoneNumber);
            if (existingUser != null)
            {
                throw new ApplicationException("Phone number is already registered.");
            }

            if (!_memoryCache.TryGetValue($"token_{request.PhoneNumber}", out string storedToken) || storedToken != otpToken)
            {
                throw new ApplicationException("Invalid or expired OTP token.");
            }

            // Xóa token khỏi cache sau khi sử dụng
            _memoryCache.Remove($"token_{request.PhoneNumber}");

            // Hash password bằng BCrypt
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newUser = new User
            {
                Fullname = request.FullName,
                Phonenumber = request.PhoneNumber,
                Password = hashedPassword,
                Dob = request.Dob,
                Gender = request.Gender,
                Role = RoleEnum.Customer.ToString(),
                Status = UserStatusEnum.Active.ToString()
            };

            // Thêm vào database
            await _unitOfWork.Repository<User>().InsertAsync(newUser);
            await _unitOfWork.SaveChangesAsync();

            // Đăng nhập tự động sau khi đăng ký thành công
            return await GenerateLoginResponse(newUser);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            // Tìm user theo số điện thoại và kiểm tra trạng thái Active
            var user = await _unitOfWork.Repository<User>().GetAsync(u => u.Phonenumber == request.PhoneNumber);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }
            if (user.Status != "Active")
            {
                throw new ApplicationException("User account is not active.");
            }

            // Kiểm tra mật khẩu
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new ApplicationException("Invalid password.");
            }

            return await GenerateLoginResponse(user);
        }

        private async Task<LoginResponse> GenerateLoginResponse(User user)
        {
            // Taoj JWT token
            string token = _jwtService.GenerateJwtToken(user);

            // Tạo refresh token (bản gốc và bản hash)
            string rawRefreshToken = Guid.NewGuid().ToString();
            string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken);

            // Cập nhật refresh token vào database
            user.Refreshtoken = hashedRefreshToken;
            user.Refreshtokenexpirytime = DateTime.UtcNow.AddDays(7);

            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Trả về thông tin đăng nhập
            return new LoginResponse
            {
                UserId = user.Userid,
                PhoneNumber = user.Phonenumber,
                FullName = user.Fullname,
                Role = user.Role,
                Rewardpoints = user.Rewardpoints,
                Token = token,
                RefreshToken = rawRefreshToken, // Trả về bản gốc (không hash)
                RefreshTokenExpiry = user.Refreshtokenexpirytime.Value
            };
        }

        public async Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken)
        {
            // Lấy danh sách người dùng từ DB
            var users = await _unitOfWork.Repository<User>().GetAllAsync();

            // Tìm user có refresh token hợp lệ
            var user = users.FirstOrDefault(u => u.Refreshtoken != null && BCrypt.Net.BCrypt.Verify(refreshToken, u.Refreshtoken));

            if (user == null)
            {
                throw new ApplicationException("Invalid refresh token.");
            }

            // Kiểm tra thời gian hết hạn của refresh token
            if (user.Refreshtokenexpirytime < DateTime.UtcNow)
            {
                throw new ApplicationException("Refresh token has expired.");
            }

            // Tạo JWT Token mới
            string newAccessToken = _jwtService.GenerateJwtToken(user);

            // Tạo Refresh Token mới
            string newRawRefreshToken = Guid.NewGuid().ToString();
            string newHashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(newRawRefreshToken);

            // Cập nhật Refresh Token trong DB
            user.Refreshtoken = newHashedRefreshToken;
            user.Refreshtokenexpirytime = DateTime.UtcNow.AddDays(7);

            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Chỉ trả về Access Token và Refresh Token mới
            return (newAccessToken, newRawRefreshToken);
        }

        public async Task LogoutAsync(Guid userId)
        {
            //Tìm user theo UserId
            var user = await _unitOfWork.Repository<User>().GetAsync(u => u.Userid == userId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            //Xóa refresh token trong database
            user.Refreshtoken = null;
            user.Refreshtokenexpirytime = null;

            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task ResetPasswordAsync(string phoneNumber, string newPassword, string otpToken)
        {
            // Kiểm tra user tồn tại
            var user = await _unitOfWork.Repository<User>().GetAsync(u => u.Phonenumber == phoneNumber);
            if (user == null)
            {
                throw new ApplicationException("User not found.");
            }

            // Kiểm tra token tạm trong cache
            if (!_memoryCache.TryGetValue($"token_{phoneNumber}", out string storedToken) || storedToken != otpToken)
            {
                throw new ApplicationException("Invalid or expired OTP token.");
            }

            // Xóa token khỏi cache sau khi sử dụng
            _memoryCache.Remove($"token_{phoneNumber}");

            // Hash mật khẩu mới bằng BCrypt
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Cập nhật password trong database
            user.Password = hashedPassword;
            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> CheckPhoneNumberExistsAsync(string phoneNumber)
        {
            if (await _unitOfWork.Repository<User>().GetAsync(u => u.Phonenumber == phoneNumber) != null) return true;
            return false;
        }
    }
}
