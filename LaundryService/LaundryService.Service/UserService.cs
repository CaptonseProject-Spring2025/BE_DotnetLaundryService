using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<UserDetailResponse> GetUserByIdAsync(Guid userId)
        {
            // Tìm user trong database
            var user = await _unitOfWork.Repository<User>().GetAsync(u => u.Userid == userId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            // Trả về thông tin user (chỉ những field cần thiết)
            return new UserDetailResponse
            {
                UserId = user.Userid,
                FullName = user.Fullname,
                PhoneNumber = user.Phonenumber,
                Email = user.Email,
                Status = user.Status,
                Avatar = user.Avatar,
                Dob = user.Dob,
                Gender = user.Gender,
                RewardPoints = user.Rewardpoints,
                DateCreated = user.Datecreated,
                DateModified = user.Datemodified
            };
        }
    }
}
