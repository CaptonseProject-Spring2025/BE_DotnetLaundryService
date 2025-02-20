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

namespace LaundryService.Service
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AuthService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<User> RegisterAsync(RegisterRequest request)
        {
            // Kiểm tra email hoặc username đã tồn tại chưa
            var existingUser = await _unitOfWork.Repository<User>().GetAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                throw new Exception("Email hoặc Username đã tồn tại.");
            }

            // Hash password bằng BCrypt
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newUser = new User
            {
                Fullname = request.Fullname,
                Email = request.Email,
                Password = hashedPassword,
                Phonenumber = request.Phonenumber,
                Role = RoleEnum.Customer.ToString(), // Gán mặc định role là Customer
                Status = UserStatusEnum.Active.ToString(),
                Emailconfirmed = false
            };

            // Thêm vào database
            await _unitOfWork.Repository<User>().InsertAsync(newUser);
            await _unitOfWork.SaveChangesAsync();

            return newUser;
        }
    }
}
