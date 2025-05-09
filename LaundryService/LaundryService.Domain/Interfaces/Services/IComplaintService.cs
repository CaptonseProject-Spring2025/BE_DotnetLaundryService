using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IComplaintService
    {
        Task CreateComplaintAsyncForCustomer(HttpContext httpContext, string orderId, string complaintDescription, string complaintType);
        Task CreateComplaintAsyncForAdminOrCustomerStaff(HttpContext httpContext, string orderId, string complaintDescription, string complaintType);
        Task<List<ComplaintResponse>> GetPendingComplaintsAsync(HttpContext httpContext);
        Task<ComplaintDetailResponse> GetComplaintDetailAsync(HttpContext httpContext, Guid complaintId);
        Task AcceptComplaintAsync(HttpContext httpContext, Guid complaintId);
        Task CompleteComplaintAsync(HttpContext httpContext, Guid complaintId, string resolutionDetails);
        Task<List<ComplaintResponse>> GetInProgressComplaintsForCustomerStaffAsync(HttpContext httpContext);
        Task<List<ComplaintResponse>> GetResolvedComplaintsForCustomerStaffAsync(HttpContext httpContext);
        Task<List<AdminComplaintResponse>> GetInProgressComplaintsForAdminAsync(HttpContext httpContext);
        Task<List<AdminComplaintResponse>> GetResolvedComplaintsForAdminAsync(HttpContext httpContext);
    }
}
