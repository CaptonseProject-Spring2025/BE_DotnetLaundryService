using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces
{
    public interface IUtil
    {
        Guid GetCurrentUserIdOrThrow(HttpContext httpContext);

        DateTime ConvertToVnTime(DateTime utcDateTime);
    }
}
