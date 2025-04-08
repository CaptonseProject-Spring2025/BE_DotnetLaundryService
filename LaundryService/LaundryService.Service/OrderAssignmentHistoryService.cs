using LaundryService.Domain.Constants;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class OrderAssignmentHistoryService : IOrderAssignmentHistoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;

        public OrderAssignmentHistoryService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }
        public async Task<List<AssignmentHistoryResponse>> GetAssignmentsForDriverAsync(HttpContext httpContext)
        {
            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);

            // Lấy assignment của tài xế
            var assignments = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAllAsync(a => a.Assignedto == driverId);

            var orderIds = assignments.Select(a => a.Orderid).Distinct().ToList();

            var orders = await _unitOfWork.Repository<Order>()
                .GetAllAsync(o => orderIds.Contains(o.Orderid));

            var userIds = orders.Select(o => o.Userid).Distinct().ToList();

            var users = await _unitOfWork.Repository<User>()
                .GetAllAsync(u => userIds.Contains(u.Userid));

            var pendingNotes = await _unitOfWork.Repository<Orderstatushistory>()
                .GetAllAsync(h => orderIds.Contains(h.Orderid) && h.Status == "PENDING");

            var notesDict = pendingNotes
                .GroupBy(h => h.Orderid)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Notes);


            // Join các dữ liệu lại
            var responses = assignments.Select(a =>
            {
                var order = orders.FirstOrDefault(o => o.Orderid == a.Orderid);
                var user = users.FirstOrDefault(u => u.Userid == order?.Userid);
                var pendingNote = notesDict.ContainsKey(a.Orderid) ? notesDict[a.Orderid] : null;

                return new AssignmentHistoryResponse
                {
                    AssignmentId = a.Assignmentid,
                    OrderId = a.Orderid,
                    Fullname = user?.Fullname,
                    Phonenumber = user?.Phonenumber,
                    Note = pendingNote,
                    AssignedAt = a.Assignedat,
                    Status = a.Status,
                    Address = GetRelevantAddress(a.Status, order)
                };
            }).ToList();

            return responses;
        }

        public async Task<AssignmentDetailResponse?> GetAssignmentDetailAsync(HttpContext httpContext, Guid assignmentId)
        {
            var driverId = _util.GetCurrentUserIdOrThrow(httpContext);

            //  Lấy assignment theo ID
            var assignmentList = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAllAsync(a => a.Assignmentid == assignmentId && a.Assignedto == driverId);
            var assignment = assignmentList.FirstOrDefault();

            if (assignment == null)
                throw new Exception("Không tìm thấy phân công hoặc không thuộc tài xế hiện tại.");

            var orderList = await _unitOfWork.Repository<Order>()
                .GetAllAsync(o => o.Orderid == assignment.Orderid);
            var order = orderList.FirstOrDefault();

            if (order == null) return null;

            var userList = await _unitOfWork.Repository<User>()
                .GetAllAsync(u => u.Userid == order.Userid);
            var user = userList.FirstOrDefault();

            var pendingNoteList = await _unitOfWork.Repository<Orderstatushistory>()
                .GetAllAsync(h => h.Orderid == order.Orderid && h.Status == "PENDING");
            var note = pendingNoteList.FirstOrDefault()?.Notes;

            return new AssignmentDetailResponse
            {
                AssignmentId = assignment.Assignmentid,
                OrderId = order.Orderid,
                Fullname = user?.Fullname,
                Phonenumber = user?.Phonenumber,
                Note = note,
                AssignedAt = assignment.Assignedat,
                Status = assignment.Status,
                PickupAddress = order.Pickupaddressdetail,
                DeliveryAddress = order.Deliveryaddressdetail,
                PickupDescription = order.Pickupdescription,
                DeliveryDescription = order.Deliverydescription,
                TotalPrice = order.Totalprice,
                CreatedAt = order.Createdat
            };
        }

        private string? GetRelevantAddress(string? status, Order? order)
        {
            if (order == null || status == null) return null;

            if (!Enum.TryParse<AssignStatusEnum>(status, out var parsedStatus))
                return null;

            if (AssignStatusGroupsConstants.Pickup.Contains(parsedStatus))
                return order.Pickupaddressdetail;

            if (AssignStatusGroupsConstants.Delivery.Contains(parsedStatus))
                return order.Deliveryaddressdetail;

            return null;
        }

    }
}
