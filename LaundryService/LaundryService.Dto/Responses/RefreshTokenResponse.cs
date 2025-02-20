using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class RefreshTokenResponse
    {
        public Guid UserId { get; set; }
        public string Token { get; set; }
    }
}
