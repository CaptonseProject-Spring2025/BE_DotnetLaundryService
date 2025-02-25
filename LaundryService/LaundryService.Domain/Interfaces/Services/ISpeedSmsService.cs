using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface ISpeedSmsService
    {
        Task<string> SendOTP(string phone);
        Task<string> ResendOTP(string phone);
        Task<bool> VerifyOTP(string phone, string otpToVerify);
        Task<string> VerifyOTPAndGenerateToken(string phone, string oTP);
    }
}
