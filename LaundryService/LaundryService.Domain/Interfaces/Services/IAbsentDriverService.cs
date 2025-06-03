using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IAbsentDriverService
    {
        Task<AbsentDriverResponse> AddAbsentAsync(AbsentDriverCreateRequest req);
        Task<AbsentDriverResponse> UpdateAbsentAsync(Guid absentId, AbsentDriverUpdateRequest req);
        Task DeleteAbsentAsync(Guid absentId);
        Task<List<AbsentDriverListResponse>> GetAllAbsentsAsync();
    }
}
