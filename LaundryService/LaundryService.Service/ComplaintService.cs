using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LaundryService.Service
{
    public class ComplaintService : IComplaintService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;
        private readonly IOrderJobService _jobService;

        public ComplaintService(IUnitOfWork unitOfWork, IUtil util, IOrderJobService jobService)
        {
            _unitOfWork = unitOfWork;
            _util = util;
            _jobService = jobService;
        }

        public async Task CreateComplaintAsyncForCustomer(HttpContext httpContext, string orderId, string complaintDescription, string complaintType)
        {
            var customerId = _util.GetCurrentUserIdOrThrow(httpContext);

            var order = await _unitOfWork.Repository<Order>().GetAsync(o => o.Orderid == orderId);
            if (order == null)
            {
                throw new KeyNotFoundException("Đơn hàng không tồn tại.");
            }

            if (order.Userid != customerId)
            {
                throw new UnauthorizedAccessException("Đơn hàng này không thuộc quyền sở hữu của bạn. Bạn không thể tạo khiếu nại cho đơn hàng này.");
            }

            if (order.Currentstatus != OrderStatusEnum.DELIVERED.ToString())
            {
                throw new InvalidOperationException("Chỉ có đơn hàng đã giao mới được phép khiếu nại.");
            }

            if (order.Currentstatus == OrderStatusEnum.COMPLAINT.ToString())
            {
                throw new InvalidOperationException("Đơn hàng này đã ở trạng thái khiếu nại. Bạn không thể tạo thêm khiếu nại.");
            }

            await _unitOfWork.BeginTransaction();
            try
            {
                if (string.IsNullOrWhiteSpace(complaintDescription))
                {
                    complaintDescription = "No description provided";
                }

                var complaint = new Complaint
                {
                    Complaintid = Guid.NewGuid(),
                    Orderid = orderId,
                    Userid = customerId,
                    Complaintdescription = complaintDescription,
                    Complainttype = complaintType,
                    Status = ComplaintStatusEnum.PENDING.ToString(),
                    Createdat = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Complaint>().InsertAsync(complaint);
                await _unitOfWork.SaveChangesAsync();

                order.Currentstatus = OrderStatusEnum.COMPLAINT.ToString();

                // Lưu jobId để lát nữa huỷ
                var jobId = order.AutoCompleteJobId;

                await _unitOfWork.Repository<Order>().UpdateAsync(order);
                await _unitOfWork.SaveChangesAsync();

                var orderStatusHistory = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = OrderStatusEnum.COMPLAINT.ToString(),
                    Statusdescription = "Customer tạo đơn khiếu nại",
                    Updatedby = customerId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(orderStatusHistory);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitTransaction();

                // Huỷ background-job (ngoài transaction DB)
                if (!string.IsNullOrEmpty(jobId))
                    _jobService.CancelAutoComplete(jobId);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransaction();
                throw new Exception("Có lỗi xảy ra khi tạo khiếu nại", ex);
            }
        }

        public async Task<List<UserComplaintResponse>> GetComplaintsForCustomerAsync(HttpContext httpContext)
        {
            var customerId = _util.GetCurrentUserIdOrThrow(httpContext);

            var complaints = await _unitOfWork.Repository<Complaint>()
                .GetAll()
                .Where(c => c.Userid == customerId)
                .OrderByDescending(c => c.Createdat)
                .ToListAsync();

            return complaints.Select(c => new UserComplaintResponse
            {
                ComplaintId = c.Complaintid,
                OrderId = c.Orderid,
                ComplaintType = c.Complainttype,
                Status = c.Status,
                CreatedAt = c.Createdat ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<UserComplaintDetailResponse> GetComplaintDetailForCustomerAsync(HttpContext httpContext, Guid complaintId)
        {
            var customerId = _util.GetCurrentUserIdOrThrow(httpContext);

            var complaint = await _unitOfWork.Repository<Complaint>()
                .GetAll()
                .Where(c => c.Complaintid == complaintId && c.Userid == customerId)
                .FirstOrDefaultAsync();

            if (complaint == null)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xem chi tiết khiếu nại này.");
            }

            return new UserComplaintDetailResponse
            {
                OrderId = complaint.Orderid,
                ComplaintType = complaint.Complainttype,
                ComplaintDescription = complaint.Complaintdescription,
                Status = complaint.Status,
                ResolutionDetails = complaint.Resolutiondetails,
                CreatedAt = complaint.Createdat ?? DateTime.MinValue,
                ResolvedAt = complaint.Resolvedat ?? DateTime.MinValue
            };
        }

        public async Task CancelComplaintAsyncForCustomer(HttpContext httpContext, Guid complaintId)
        {
            var customerId = _util.GetCurrentUserIdOrThrow(httpContext);

            var complaint = await _unitOfWork.Repository<Complaint>()
                .GetAsync(c => c.Complaintid == complaintId);
            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

            if (complaint.Userid != customerId)
                throw new UnauthorizedAccessException("Bạn không có quyền hủy khiếu nại này.");

            if (complaint.Status != ComplaintStatusEnum.PENDING.ToString())
                throw new InvalidOperationException("Chỉ khi khiếu nại ở trạng thái PENDING mới được hủy.");

            var order = await _unitOfWork.Repository<Order>()
                .GetAsync(o => o.Orderid == complaint.Orderid);
            if (order == null)
                throw new KeyNotFoundException("Đơn hàng liên quan không tồn tại.");

            await _unitOfWork.BeginTransaction();
            try
            {
                complaint.Status = ComplaintStatusEnum.CANCELLED.ToString();
                await _unitOfWork.Repository<Complaint>().UpdateAsync(complaint);

                order.Currentstatus = OrderStatusEnum.DELIVERED.ToString();
                await _unitOfWork.Repository<Order>().UpdateAsync(order);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task CreateComplaintAsyncForAdminOrCustomerStaff(HttpContext httpContext, string orderId, string complaintDescription, string complaintType)
        {
            var adminOrCustomerStaffId = _util.GetCurrentUserIdOrThrow(httpContext);

            var order = await _unitOfWork.Repository<Order>().GetAsync(o => o.Orderid == orderId);
            if (order == null)
            {
                throw new KeyNotFoundException("Đơn hàng không tồn tại.");
            }

            if (order.Currentstatus == OrderStatusEnum.COMPLAINT.ToString())
            {
                throw new InvalidOperationException("Đơn hàng này đã ở trạng thái khiếu nại. Bạn không thể tạo thêm khiếu nại.");
            }

            var customerId = order.Userid;

            await _unitOfWork.BeginTransaction();
            try
            {
                if (string.IsNullOrWhiteSpace(complaintDescription))
                {
                    complaintDescription = "No description provided";
                }

                var complaint = new Complaint
                {
                    Complaintid = Guid.NewGuid(),
                    Orderid = orderId,
                    Userid = customerId,
                    Complaintdescription = complaintDescription,
                    Complainttype = complaintType,
                    Status = ComplaintStatusEnum.PENDING.ToString(),
                    Createdat = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Complaint>().InsertAsync(complaint);
                await _unitOfWork.SaveChangesAsync();

                order.Currentstatus = OrderStatusEnum.COMPLAINT.ToString();
                await _unitOfWork.Repository<Order>().UpdateAsync(order);
                await _unitOfWork.SaveChangesAsync();

                var orderStatusHistory = new Orderstatushistory
                {
                    Statushistoryid = Guid.NewGuid(),
                    Orderid = orderId,
                    Status = OrderStatusEnum.COMPLAINT.ToString(),
                    Statusdescription = "Admin hoặc CustomerStaff tạo đơn khiếu nại",
                    Notes = complaintDescription,
                    Updatedby = adminOrCustomerStaffId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Orderstatushistory>().InsertAsync(orderStatusHistory);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitTransaction();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransaction();
                throw new Exception("Có lỗi xảy ra khi tạo khiếu nại", ex);
            }
        }

        public async Task<List<ComplaintResponse>> GetPendingComplaintsAsync(HttpContext httpContext)
        {

            var complaints = await _unitOfWork.Repository<Complaint>()
                .GetAll()
                .Where(c => c.Status == ComplaintStatusEnum.PENDING.ToString())
                .OrderBy(c => c.Createdat)
                .Include(c => c.User)
                .ToListAsync();

            return complaints.Select(c => new ComplaintResponse
            {
                ComplaintId = c.Complaintid,
                OrderId = c.Orderid,
                FullName = c.User?.Fullname,
                ComplaintType = c.Complainttype,
                Status = c.Status,
                CreatedAt = c.Createdat ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<ComplaintDetailResponse> GetComplaintDetailAsync(HttpContext httpContext, Guid complaintId)
        {

            var complaint = await _unitOfWork.Repository<Complaint>()
                .GetAll()
                .Where(c => c.Complaintid == complaintId)
                .Include(c => c.User)
                .Include(c => c.Order)
                .Include(c => c.AssignedtoNavigation)
                .FirstOrDefaultAsync();

            if (complaint == null)
            {
                throw new KeyNotFoundException("Không tìm thấy khiếu nại với ID này.");
            }

            return new ComplaintDetailResponse
            {
                OrderId = complaint.Orderid,
                FullName = complaint.User?.Fullname,
                PhoneNumber = complaint.User?.Phonenumber,
                PickupAddressDetail = complaint.Order?.Pickupaddressdetail,
                DeliveryAddressDetail = complaint.Order?.Deliveryaddressdetail,
                OrderCreatedAt = complaint.Order?.Createdat ?? DateTime.MinValue,
                ComplaintType = complaint.Complainttype,
                ComplaintDescription = complaint.Complaintdescription,
                CreatedAt = complaint.Createdat ?? DateTime.MinValue,
                HandlerName = complaint.AssignedtoNavigation?.Fullname,
                ResolutionDetails = complaint.Resolutiondetails,
                ResolvedAt = complaint.Resolvedat ?? DateTime.MinValue
            };
        }

        public async Task AcceptComplaintAsync(HttpContext httpContext, Guid complaintId)
        {
            var adminOrStaffId = _util.GetCurrentUserIdOrThrow(httpContext);

            var complaint = await _unitOfWork.Repository<Complaint>().GetAsync(c => c.Complaintid == complaintId);
            if (complaint == null)
            {
                throw new KeyNotFoundException("Không tìm thấy khiếu nại với ID này.");
            }

            if (complaint.Status == ComplaintStatusEnum.IN_PROGRESS.ToString())
            {
                throw new InvalidOperationException("Khiếu nại này đã được nhận xử lý.");
            }

            await _unitOfWork.BeginTransaction();
            try
            {
                complaint.Assignedto = adminOrStaffId;
                complaint.Status = ComplaintStatusEnum.IN_PROGRESS.ToString();

                await _unitOfWork.Repository<Complaint>().UpdateAsync(complaint);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitTransaction();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransaction();
                throw new Exception("Có lỗi xảy ra khi nhận xử lý khiếu nại", ex);
            }
        }

        public async Task CompleteComplaintAsync(HttpContext httpContext, Guid complaintId, string resolutionDetails)
        {
            var userId = _util.GetCurrentUserIdOrThrow(httpContext);

            var complaint = await _unitOfWork.Repository<Complaint>().GetAsync(c => c.Complaintid == complaintId);
            if (complaint == null)
            {
                throw new KeyNotFoundException("Không tìm thấy khiếu nại với ID này.");
            }

            if (complaint.Status != ComplaintStatusEnum.IN_PROGRESS.ToString())
            {
                throw new InvalidOperationException("Chỉ khiếu nại có trạng thái IN_PROGRESS mới có thể hoàn thành.");
            }

            if (httpContext.User.IsInRole("CustomerStaff") && complaint.Assignedto != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền hoàn thành khiếu nại này.");
            }

            await _unitOfWork.BeginTransaction();
            try
            {
                complaint.Status = ComplaintStatusEnum.RESOLVED.ToString();
                complaint.Resolutiondetails = resolutionDetails;
                complaint.Resolvedat = DateTime.UtcNow;

                await _unitOfWork.Repository<Complaint>().UpdateAsync(complaint);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitTransaction();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransaction();
                throw new Exception("Có lỗi xảy ra khi hoàn thành khiếu nại", ex);
            }
        }

        public async Task<List<ComplaintResponse>> GetInProgressComplaintsForCustomerStaffAsync(HttpContext httpContext)
        {
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            var complaints = await _unitOfWork.Repository<Complaint>()
                .GetAll()
                .Where(c => c.Status == ComplaintStatusEnum.IN_PROGRESS.ToString() && c.Assignedto == staffId)
                .OrderByDescending(c => c.Createdat)
                .Include(c => c.User)
                .ToListAsync();

            return complaints.Select(c => new ComplaintResponse
            {
                ComplaintId = c.Complaintid,
                OrderId = c.Orderid,
                FullName = c.User?.Fullname,
                ComplaintType = c.Complainttype,
                Status = c.Status,
                CreatedAt = c.Createdat ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<List<ComplaintResponse>> GetResolvedComplaintsForCustomerStaffAsync(HttpContext httpContext)
        {
            var staffId = _util.GetCurrentUserIdOrThrow(httpContext);

            var complaints = await _unitOfWork.Repository<Complaint>()
                .GetAll()
                .Where(c => c.Status == ComplaintStatusEnum.RESOLVED.ToString() && c.Assignedto == staffId)
                .OrderByDescending(c => c.Createdat)
                .Include(c => c.User)
                .ToListAsync();

            return complaints.Select(c => new ComplaintResponse
            {
                ComplaintId = c.Complaintid,
                OrderId = c.Orderid,
                FullName = c.User?.Fullname,
                ComplaintType = c.Complainttype,
                Status = c.Status,
                CreatedAt = c.Createdat ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<List<AdminComplaintResponse>> GetInProgressComplaintsForAdminAsync(HttpContext httpContext)
        {
            var complaints = await _unitOfWork.Repository<Complaint>()
                .GetAll()
                .Where(c => c.Status == ComplaintStatusEnum.IN_PROGRESS.ToString())
                .OrderByDescending(c => c.Createdat)
                .Include(c => c.User)
                .Include(c => c.AssignedtoNavigation)
                .ToListAsync();

            return complaints.Select(c => new AdminComplaintResponse
            {
                ComplaintId = c.Complaintid,
                OrderId = c.Orderid,
                FullName = c.User?.Fullname,
                ComplaintType = c.Complainttype,
                Status = c.Status,
                CreatedAt = c.Createdat ?? DateTime.MinValue,
                HandlerName = c.AssignedtoNavigation?.Fullname
            }).ToList();
        }

        public async Task<List<AdminComplaintResponse>> GetResolvedComplaintsForAdminAsync(HttpContext httpContext)
        {
            var complaints = await _unitOfWork.Repository<Complaint>()
                .GetAll() 
                .Where(c => c.Status == ComplaintStatusEnum.RESOLVED.ToString())
                .OrderByDescending(c => c.Createdat)
                .Include(c => c.User) 
                .Include(c => c.AssignedtoNavigation) 
                .ToListAsync();

            return complaints.Select(c => new AdminComplaintResponse
            {
                ComplaintId = c.Complaintid,
                OrderId = c.Orderid,
                FullName = c.User?.Fullname,
                ComplaintType = c.Complainttype,
                Status = c.Status,
                CreatedAt = c.Createdat ?? DateTime.MinValue,
                HandlerName = c.AssignedtoNavigation?.Fullname
            }).ToList();
        }
    }
}
