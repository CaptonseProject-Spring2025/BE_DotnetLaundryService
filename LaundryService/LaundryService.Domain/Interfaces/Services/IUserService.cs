using LaundryService.Dto.Pagination;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
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

        Task<UserDetailResponse> UpdateUserProfileAsync(UpdateUserProfileRequest request);

        Task<bool> DeleteUserAsync(Guid userId);

        Task<bool> CheckPhoneNumberExistsAsync(string phoneNumber);

        Task<PaginationResult<UserDetailResponse>> GetUsersAsync(HttpContext httpContext, string? role, int page, int pageSize);

        Task<UserDetailResponse> CreateUserAsync(CreateUserRequest request);
    }
}
