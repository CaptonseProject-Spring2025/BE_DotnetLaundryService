using System;
using LaundryService.Domain.Entities;
using LaundryService.Dto.Requests;
using Microsoft.AspNetCore.Http;

namespace LaundryService.Domain.Interfaces.Services;

public interface IExcelService
{
    Task<byte[]> ExportUsersToExcel();

    Task<List<User>> ImportUsersFromExcel(IFormFile file);

    Task<byte[]> ExportLaundryServicesToExcel();

    Task<ImportLaundryResult> ImportLaundryServicesFromExcel(IFormFile file);
}
