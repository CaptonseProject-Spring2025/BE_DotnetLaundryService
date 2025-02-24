using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class VerifyOtpRequest
    {
        public string Phone { get; set; }
        public string OTP { get; set; }
    }
}
