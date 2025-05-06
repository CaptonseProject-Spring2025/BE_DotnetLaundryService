using System;
using LaundryService.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace LaundryService.Domain.Interfaces.Services;

public interface IExcelService
{
  Task<byte[]> ExportUsersToExcel();

  Task<List<User>> ImportUsersFromExcel(IFormFile file);

    Task<byte[]> ExportLaundryServicesToExcel();
}
