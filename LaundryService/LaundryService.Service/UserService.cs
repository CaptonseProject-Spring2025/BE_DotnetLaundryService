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
using LaundryService.Dto.Pagination;
using LaundryService.Infrastructure;
using DocumentFormat.OpenXml.Spreadsheet;

namespace LaundryService.Service
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IFileStorageService _fileStorageService;

        public UserService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _util = util;
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
                DateCreated = user.Datecreated != null ? _util.ConvertToVnTime(user.Datecreated.Value) : null,
                DateModified = user.Datemodified != null ? _util.ConvertToVnTime(user.Datemodified.Value) : null
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
            user.Datemodified = DateTime.UtcNow;

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
                DateCreated = user.Datecreated != null ? _util.ConvertToVnTime(user.Datecreated.Value) : null,
                DateModified = user.Datemodified != null ? _util.ConvertToVnTime(user.Datemodified.Value) : null
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

        public async Task<PaginationResult<UserDetailResponse>> GetUsersAsync(HttpContext httpContext, string? role, int page, int pageSize)
        {
            var currentUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            var usersQuery = _unitOfWork.Repository<User>().GetAll().Where(u => u.Userid != currentUserId);

            if (!string.IsNullOrEmpty(role))
            {
                usersQuery = usersQuery.Where(u => u.Role == role);
            }

            usersQuery = usersQuery.OrderBy(u => u.Datecreated); // Sắp xếp theo CreatedAt

            var paginatedUsers = await usersQuery
                .Select(u => new UserDetailResponse
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
                    DateCreated = u.Datecreated != null ? _util.ConvertToVnTime(u.Datecreated.Value) : null,
                    DateModified = u.Datemodified != null ? _util.ConvertToVnTime(u.Datemodified.Value) : null
                })
            .ToPagedListAsync(page, pageSize);

            return paginatedUsers;
        }

        public async Task<UserDetailResponse> CreateUserAsync(CreateUserRequest request)
        {
            // 1. Kiểm tra xem số điện thoại đã tồn tại trong DB chưa
            var existingUserByPhone = await _unitOfWork.Repository<User>()
                .GetAsync(u => u.Phonenumber == request.PhoneNumber);

            if (existingUserByPhone != null)
            {
                throw new ApplicationException("Phone number is already in use.");
            }

            // 2. Nếu có email, kiểm tra email đã tồn tại chưa (nếu rỗng thì bỏ qua)
            if (!string.IsNullOrEmpty(request.Email))
            {
                var existingUserByEmail = await _unitOfWork.Repository<User>()
                    .GetAsync(u => u.Email == request.Email);
                if (existingUserByEmail != null)
                {
                    throw new ApplicationException("Email is already in use.");
                }
            }

            // 3. Hash password
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 4. Xử lý upload avatar (nếu có)
            string? avatarUrl = null;
            if (request.Avatar != null)
            {
                avatarUrl = await _fileStorageService.UploadFileAsync(request.Avatar, "user-avatars");
            }

            // 5. Tạo user entity
            var newUser = new User
            {
                Fullname = request.FullName,
                Email = request.Email,
                Password = hashedPassword,
                Role = request.Role,
                Avatar = avatarUrl,
                Dob = request.Dob,
                Gender = request.Gender,
                Phonenumber = request.PhoneNumber,
                Rewardpoints = request.RewardPoints ?? 0, // nếu null thì set 0
                Status = "Active",
                Datecreated = DateTime.UtcNow
            };

            // 6. Lưu vào DB
            await _unitOfWork.Repository<User>().InsertAsync(newUser);
            await _unitOfWork.SaveChangesAsync();

            // 7. Trả về UserDetailResponse
            var response = new UserDetailResponse
            {
                UserId = newUser.Userid,
                FullName = newUser.Fullname,
                PhoneNumber = newUser.Phonenumber,
                Email = newUser.Email,
                Role = newUser.Role,
                Status = newUser.Status,
                Avatar = newUser.Avatar,
                Dob = newUser.Dob,
                Gender = newUser.Gender,
                RewardPoints = newUser.Rewardpoints,
                DateCreated = newUser.Datecreated != null ? _util.ConvertToVnTime(newUser.Datecreated.Value) : null,
                DateModified = newUser.Datemodified != null ? _util.ConvertToVnTime(newUser.Datemodified.Value) : null
            };

            return response;
        }
    }
}
