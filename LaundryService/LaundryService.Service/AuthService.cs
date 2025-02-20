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

namespace LaundryService.Service
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;

        public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            // Kiểm tra số điện thoại đã tồn tại chưa
            var existingUser = await _unitOfWork.Repository<User>().GetAsync(u => u.Phonenumber == request.PhoneNumber);
            if (existingUser != null)
            {
                throw new ApplicationException("Phone number is already registered.");
            }

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
            // Sinh JWT token
            string token = _jwtService.GenerateJwtToken(user);

            // Tạo refresh token (bản gốc và bản hash)
            string rawRefreshToken = Guid.NewGuid().ToString();
            string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken);

            // Cập nhật refresh token vào database
            user.Refreshtoken = hashedRefreshToken;
            user.Refreshtokenexpirytime = DateTime.Now.AddDays(7);

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

        public async Task<User> GetUserByPhoneNumberAsync(string phoneNumber)
        {
            var user = await _unitOfWork.Repository<User>().GetAsync(u => u.Phonenumber == phoneNumber);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }
            return user;
        }

    }
}
