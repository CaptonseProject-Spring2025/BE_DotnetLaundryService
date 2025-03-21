using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class AddressService : IAddressService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapboxService _mapboxService;

        public AddressService(IUnitOfWork unitOfWork, IMapboxService mapboxService)
        {
            _unitOfWork = unitOfWork;
            _mapboxService = mapboxService;
        }

        public async Task<AddressResponse> CreateAddressAsync(HttpContext httpContext, CreateAddressRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Lấy UserId từ JWT token
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user token.");
            }

            if (string.IsNullOrWhiteSpace(request.DetailAddress))
                throw new ArgumentException("DetailAddress is required.");

            // Gọi MapboxService để lấy tọa độ từ địa chỉ
            var (fetchedLatitude, fetchedLongitude) = await _mapboxService.GetCoordinatesFromAddressAsync(request.DetailAddress);

            // Tính khoảng cách
            double distance = _mapboxService.CalculateDistance(request.Latitude, request.Longitude, fetchedLatitude, fetchedLongitude);
            if (distance > 1000)
                throw new ArgumentException($"The location is too far from the entered address. Difference: {distance:F2} meters.");

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
                    Datecreated = DateTime.Now
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
            // Lấy UserId từ JWT token
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user token.");
            }

            // Tìm địa chỉ theo ID
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
    }
}
