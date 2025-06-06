﻿using Amazon.Runtime.Internal;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class SpeedSmsService : ISpeedSmsService
    {
        private readonly string _rootUrl;
        private readonly string _accessToken;
        private readonly string _senderToken;
        private readonly IMemoryCache _memoryCache;

        public SpeedSmsService(IConfiguration configuration, IMemoryCache memoryCache)
        {
            _rootUrl = configuration["SpeedSMS:RootUrl"];
            _accessToken = configuration["SpeedSMS:AccessToken"];
            _senderToken = configuration["SpeedSMS:SenderToken"];
            _memoryCache = memoryCache;
        }

        public string GenerateOTP(int length = 6)
        {
            Random random = new Random();
            string otp = string.Empty;
            for (int i = 0; i < length; i++)
            {
                otp += random.Next(0, 10).ToString();
            }
            return otp;
        }

        public async Task<string> SendOTP(string phone)
        {
            try
            {
                string otp = GenerateOTP();
                string content = $"[EcoLaundry] Mã OTP chỉ có hiệu lực trong 5 phút; Vui lòng không chia sẻ mã này với bất kỳ ai; Mã của bạn là: {otp}";

                // Lưu OTP vào bộ nhớ tạm với thời gian sống là 5 phút (300 giây)
                _memoryCache.Set(phone, otp, TimeSpan.FromMinutes(5));

                // Gửi SMS OTP
                return await SendSMS(new string[] { phone }, content, 5, _senderToken);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while sending SMS: " + ex.Message);
            }
        }

        public async Task<string> ResendOTP(string phone)
        {
            try
            {
                // Xóa OTP cũ nếu có
                _memoryCache.Remove(phone);

                // Gửi lại OTP mới và lưu vào bộ nhớ tạm
                return await SendOTP(phone);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while resending OTP: " + ex.Message);
            }
        }

        public async Task<string> SendSMS(string[] phones, string content, int type, string sender)
        {
            if (phones.Length <= 0 || string.IsNullOrEmpty(content)) return "";
            if (type == 3 && string.IsNullOrEmpty(sender)) return "";

            try
            {
                NetworkCredential myCreds = new NetworkCredential(_accessToken, ":x");
                WebClient client = new WebClient();
                client.Credentials = myCreds;
                client.Headers[HttpRequestHeader.ContentType] = "application/json";

                string builder = "{\"to\":[";

                for (int i = 0; i < phones.Length; i++)
                {
                    builder += $"\"{phones[i]}\"";
                    if (i < phones.Length - 1) builder += ",";
                }
                builder += $"], \"content\": \"{Uri.EscapeDataString(content)}\", \"type\":{type}, \"sender\": \"{sender}\"}}";

                string json = builder;
                return await Task.FromResult(client.UploadString($"{_rootUrl}/sms/send", json));
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while sending SMS: " + ex.Message);
            }
        }

        public async Task<bool> VerifyOTP(string phone, string otpToVerify)
        {
            try
            {
                if (_memoryCache.TryGetValue(phone, out string storedOtp))
                {
                    if (storedOtp == otpToVerify)
                    {
                        // Xóa OTP khỏi bộ nhớ tạm sau khi xác minh thành công
                        _memoryCache.Remove(phone);
                        return true;
                    }
                    else
                    {
                        throw new ApplicationException("OTP does not match.");
                    }
                }
                else
                {
                    throw new ApplicationException("OTP not found or expired.");
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while verifying OTP: " + ex.Message);
            }
        }

        public async Task<string> VerifyOTPAndGenerateToken(string phone, string otpToVerify)
        {
            try
            {
                if (_memoryCache.TryGetValue(phone, out string storedOtp))
                {
                    if (storedOtp == otpToVerify)
                    {
                        // Xóa OTP khỏi cache sau khi xác minh thành công
                        _memoryCache.Remove(phone);

                        // Tạo token ngẫu nhiên
                        string token = Guid.NewGuid().ToString();

                        // Lưu token vào cache với thời gian tồn tại 5 phút
                        _memoryCache.Set($"token_{phone}", token, TimeSpan.FromMinutes(5));

                        return token;
                    }
                    else
                    {
                        throw new ApplicationException("OTP does not match.");
                    }
                }
                else
                {
                    throw new ApplicationException("OTP not found or expired.");
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error while verifying OTP: " + ex.Message);
            }
        }
    }
}
