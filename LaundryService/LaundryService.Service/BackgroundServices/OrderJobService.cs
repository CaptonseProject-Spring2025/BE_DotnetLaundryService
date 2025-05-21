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

        public void ScheduleAutoComplete(string orderId, DateTime deliveredAtUtc)
        {
            var id = JobId(orderId);
            _bg.Delete(id); // nếu đã tồn tại -> huỷ trước
            _bg.Schedule<OrderAutoCompleteWorker>(
                id,
                x => x.ExecuteAsync(orderId),
                deliveredAtUtc.AddHours(48) - DateTime.UtcNow);
        }

        public void CancelAutoComplete(string orderId) => _bg.Delete(JobId(orderId));
    }
}
