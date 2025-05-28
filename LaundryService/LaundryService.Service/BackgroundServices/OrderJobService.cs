using Hangfire;
using LaundryService.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service.BackgroundServices
{
    public class OrderJobService : IOrderJobService
    {
        private readonly IBackgroundJobClient _bg;
        private static string JobId(string orderId) => $"auto-complete:{orderId}";

        public OrderJobService(IBackgroundJobClient bg) => _bg = bg;

        public string ScheduleAutoComplete(string orderId, DateTime deliveredAtUtc)
        {
            //delay
            var jobId = _bg.Schedule<OrderAutoCompleteWorker>(
                            x => x.ExecuteAsync(orderId),
                            deliveredAtUtc.AddHours(48) - DateTime.UtcNow);

            return jobId;
        }

        public void CancelAutoComplete(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return;

            //Delete() trả bool, ko throw exception nếu ko tìm thấy job
            // chưa kiểm tra jobId có tồn tại ko
            BackgroundJob.Delete(jobId);
        }
    }
}
