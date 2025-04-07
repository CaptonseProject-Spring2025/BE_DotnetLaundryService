using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IOrderAssignmentHistoryService
    {
        Task<List<AssignmentHistoryResponse>> GetAssignmentsForDriverAsync(HttpContext httpContext);
    }
}
