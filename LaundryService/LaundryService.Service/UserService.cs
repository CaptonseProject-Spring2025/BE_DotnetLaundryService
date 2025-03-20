using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace LaundryService.Service
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;

        public UserService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
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
                Role = user.Role,
                Status = user.Status,
                Avatar = user.Avatar,
                Dob = user.Dob,
                Gender = user.Gender,
                RewardPoints = user.Rewardpoints,
                DateCreated = user.Datecreated,
                DateModified = user.Datemodified
            };
        }

        public async Task<UserDetailResponse> UpdateUserProfileAsync(UpdateUserProfileRequest request)
        {
            // Tìm user theo UserId
            var user = await _unitOfWork.Repository<User>().GetAsync(u => u.Userid == request.UserId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            // Cập nhật Fullname nếu có dữ liệu
            if (!string.IsNullOrEmpty(request.FullName))
            {
                user.Fullname = request.FullName;
            }

            // Cập nhật Email nếu có dữ liệu
            if (!string.IsNullOrEmpty(request.Email))
            {
                var existingUser = await _unitOfWork.Repository<User>().GetAsync(u => u.Email == request.Email && u.Userid != request.UserId);
                if (existingUser != null)
                {
                    throw new ApplicationException("Email is already in use.");
                }
                user.Email = request.Email;
            }

            // Cập nhật Ngày sinh (DOB) nếu có dữ liệu
            if (request.Dob.HasValue)
            {
                user.Dob = request.Dob.Value;
            }

            // Cập nhật giới tính nếu có dữ liệu
            if (!string.IsNullOrEmpty(request.Gender))
            {
                user.Gender = request.Gender;
            }

            // Xử lý avatar nếu có file tải lên
            if (request.Avatar != null)
            {
                // Nếu đã có ảnh cũ, xóa khỏi B2 storage
                if (!string.IsNullOrEmpty(user.Avatar))
                {
                    await _fileStorageService.DeleteFileAsync(user.Avatar);
                }

                // Upload ảnh mới lên B2 storage
                var avatarUrl = await _fileStorageService.UploadFileAsync(request.Avatar, "user-avatars");
                user.Avatar = avatarUrl;
            }

            // Cập nhật thời gian chỉnh sửa
            user.Datemodified = DateTime.Now;

            // Lưu vào database
            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Trả về thông tin user sau khi cập nhật
            return new UserDetailResponse
            {
                UserId = user.Userid,
                FullName = user.Fullname,
                PhoneNumber = user.Phonenumber,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                Avatar = user.Avatar,
                Dob = user.Dob,
                Gender = user.Gender,
                RewardPoints = user.Rewardpoints,
                DateCreated = user.Datecreated,
                DateModified = user.Datemodified
            };
        }

        public async Task<bool> DeleteUserAsync(Guid userId)
        {
            // Tìm user theo UserId
            var user = await _unitOfWork.Repository<User>().GetAsync(u => u.Userid == userId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            // Cập nhật trạng thái thành "Deleted"
            user.Status = "Deleted";

            // Xóa RefreshToken và RefreshTokenExpiryTime
            user.Refreshtoken = null;
            user.Refreshtokenexpirytime = null;

            // Lưu thay đổi vào database
            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CheckPhoneNumberExistsAsync(string phoneNumber)
        {
            if (await _unitOfWork.Repository<User>().GetAsync(u => u.Phonenumber == phoneNumber) != null) return true;
            return false;
        }

        public Guid GetCurrentUserIdOrThrow(HttpContext httpContext)
        {
            var userIdClaim = httpContext?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
            {
                return userId;
            }
            throw new UnauthorizedAccessException("Invalid token");
        }

        public async Task<IEnumerable<UserDetailResponse>> GetUsersAsync(HttpContext httpContext, string? role)
        {
            var currentUserId = GetCurrentUserIdOrThrow(httpContext);

            var usersQuery = _unitOfWork.Repository<User>().GetAll().Where(u => u.Userid != currentUserId);

            if (!string.IsNullOrEmpty(role))
            {
                usersQuery = usersQuery.Where(u => u.Role == role);
            }

            var users = await Task.FromResult(usersQuery.ToList());

            return users.Select(u => new UserDetailResponse
            {
                UserId = u.Userid,
                FullName = u.Fullname,
                PhoneNumber = u.Phonenumber,
                Email = u.Email,
                Role = u.Role,
                Status = u.Status,
                Avatar = u.Avatar,
                Dob = u.Dob,
                Gender = u.Gender,
                RewardPoints = u.Rewardpoints,
                DateCreated = u.Datecreated,
                DateModified = u.Datemodified
            }).ToList();
        }
    }
}
