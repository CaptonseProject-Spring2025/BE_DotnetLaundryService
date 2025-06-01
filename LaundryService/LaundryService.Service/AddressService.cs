using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using LaundryService.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class AddressService : IAddressService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapboxService _mapboxService;
        private readonly IUtil _util;

        public AddressService(IUnitOfWork unitOfWork, IMapboxService mapboxService, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _mapboxService = mapboxService;
            _util = util;
        }

        public async Task<AddressResponse> CreateAddressAsync(HttpContext httpContext, CreateAddressRequest request)
        {
            //lấy userId từ jwt token
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user token.");
            }

            return await CreateAddressAsync(userId, request);
        }

        public async Task<AddressResponse> CreateAddressAsync(Guid userId, CreateAddressRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.DetailAddress))
                throw new ArgumentException("DetailAddress is required.");

            //// Gọi MapboxService để lấy tọa độ từ địa chỉ
            //var (fetchedLatitude, fetchedLongitude) = await _mapboxService.GetCoordinatesFromAddressAsync(request.DetailAddress);

            //// Tính khoảng cách
            //double distance = _mapboxService.CalculateDistance(request.Latitude, request.Longitude, fetchedLatitude, fetchedLongitude);
            //if (distance > 3000)
            //    throw new ArgumentException($"The location is too far from the entered address. Difference: {distance:F2} meters.");

            // Bắt đầu Transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                var address = new Address
                {
                    Userid = userId,
                    Detailaddress = request.DetailAddress,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    Addresslabel = request.AddressLabel,
                    Contactname = request.ContactName,
                    Contactphone = request.ContactPhone,
                    Description = request.Description,
                    Datecreated = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Address>().InsertAsync(address);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                return new AddressResponse
                {
                    AddressId = address.Addressid,
                    DetailAddress = address.Detailaddress,
                    Latitude = address.Latitude.Value,
                    Longitude = address.Longitude.Value,
                    AddressLabel = address.Addresslabel,
                    ContactName = address.Contactname,
                    ContactPhone = address.Contactphone,
                    Description = address.Description,
                    DateCreated = address.Datecreated.Value
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<bool> DeleteAddressAsync(HttpContext httpContext, Guid addressId)
        {
            //lấy userId từ jwt token
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user token.");
            }

            // Tìm địa chỉ theo id
            var address = await _unitOfWork.Repository<Address>().GetAsync(a => a.Addressid == addressId && a.Userid == userId);
            if (address == null)
            {
                throw new KeyNotFoundException("Address not found or does not belong to the user.");
            }

            // Bắt đầu Transaction
            await _unitOfWork.BeginTransaction();
            try
            {
                await _unitOfWork.Repository<Address>().DeleteAsync(address);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransaction();

                return true;
            }
            catch
            {
                await _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<List<AddressResponse>> GetUserAddressesAsync(HttpContext httpContext)
        {
            //lấy userId từ jwt token
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user token.");
            }

            // Lấy danh sách địa chỉ của User từ database
            var addresses = await _unitOfWork.Repository<Address>().GetAllAsync(a => a.Userid == userId);
            if (!addresses.Any())
            {
                throw new KeyNotFoundException("No addresses found for this user.");
            }

            return addresses.Select(a => new AddressResponse
            {
                AddressId = a.Addressid,
                DetailAddress = a.Detailaddress,
                Latitude = a.Latitude ?? 0,
                Longitude = a.Longitude ?? 0,
                AddressLabel = a.Addresslabel,
                ContactName = a.Contactname,
                ContactPhone = a.Contactphone,
                Description = a.Description,
                DateCreated = a.Datecreated ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<AddressResponse> GetAddressByIdAsync(HttpContext httpContext, Guid addressId)
        {
            // Lấy UserId từ JWT token
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user token.");
            }

            // Tìm địa chỉ theo id
            var address = await _unitOfWork.Repository<Address>().GetAsync(a => a.Addressid == addressId && a.Userid == userId);
            if (address == null)
            {
                throw new KeyNotFoundException("Address not found or does not belong to the user.");
            }

            // retủn thông tin chi tiết của địa chỉ
            return new AddressResponse
            {
                AddressId = address.Addressid,
                DetailAddress = address.Detailaddress,
                Latitude = address.Latitude ?? 0,
                Longitude = address.Longitude ?? 0,
                AddressLabel = address.Addresslabel,
                ContactName = address.Contactname,
                ContactPhone = address.Contactphone,
                Description = address.Description,
                DateCreated = address.Datecreated ?? DateTime.MinValue
            };
        }

        public async Task<AddressInfoResponse> GetPickupAddressFromAssignmentAsync(HttpContext httpContext, Guid assignmentId)
        {
            var currentUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            var assignment = _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Include(a => a.Order)
                .FirstOrDefault(a => a.Assignmentid == assignmentId);

            if (assignment == null)
                throw new KeyNotFoundException("Assignment not found.");

            if (assignment.Assignedto != currentUserId)
                throw new UnauthorizedAccessException("You are not assigned to this order.");

            var order = assignment.Order;

            return new AddressInfoResponse
            {
                AddressDetail = order.Pickupaddressdetail,
                Latitude = order.Pickuplatitude,
                Longitude = order.Pickuplongitude
            };
        }

        public async Task<AddressInfoResponse> GetDeliveryAddressFromAssignmentAsync(HttpContext httpContext, Guid assignmentId)
        {
            var currentUserId = _util.GetCurrentUserIdOrThrow(httpContext);

            var assignment = await _unitOfWork.Repository<Orderassignmenthistory>()
                .GetAll()
                .Include(a => a.Order)
                .FirstOrDefaultAsync(a => a.Assignmentid == assignmentId);

            if (assignment == null)
                throw new KeyNotFoundException("Assignment not found.");

            if (assignment.Assignedto != currentUserId)
                throw new UnauthorizedAccessException("You are not assigned to this order.");

            var order = assignment.Order;

            return new AddressInfoResponse
            {
                AddressDetail = order.Deliveryaddressdetail,
                Latitude = order.Deliverylatitude,
                Longitude = order.Deliverylongitude
            };
        }

    }
}
