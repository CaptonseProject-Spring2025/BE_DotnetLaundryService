using System;

namespace LaundryService.Domain.Interfaces.Services;

public interface IExcelService
{
  Task<byte[]> ExportUsersToExcel();
}
