using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Enums;

namespace LaundryService.Service
{
    public class TrackingPermissionService : ITrackingPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TrackingPermissionService(IUnitOfWork unitOfWork)
            => _unitOfWork = unitOfWork;

        public async Task<bool> CanDriverTrackAsync(string orderId, Guid driverId)
        {
            return await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAllAsync(a =>
                    a.Orderid == orderId &&
                    (a.Status == AssignStatusEnum.ASSIGNED_PICKUP.ToString()
                      || a.Status == AssignStatusEnum.ASSIGNED_DELIVERY.ToString()))
                .ContinueWith(t => t.Result.Any(a => a.Assignedto == driverId));
        }

        public async Task<bool> CanCustomerViewAsync(string orderId, Guid customerId)
        {
            var order = await _unitOfWork.Repository<Order>()
                .GetAllAsync(o => o.Orderid == orderId)
                .ContinueWith(t => t.Result.FirstOrDefault());
            return order != null && order.Userid == customerId;
        }
    }

}
