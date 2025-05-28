using Hangfire;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Enums;
using LaundryService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service.BackgroundServices
{
    /// <summary>
    /// job chạy 48h để tự động hoàn tất đơn hàng đã nhận
    /// </summary>
    public class OrderAutoCompleteWorker
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<OrderAutoCompleteWorker> _logger;

        public OrderAutoCompleteWorker(IUnitOfWork uow, ILogger<OrderAutoCompleteWorker> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 2)]
        public async Task ExecuteAsync(string orderId)
        {
            var orderRepo = _uow.Repository<Order>();
            var historyRepo = _uow.Repository<Orderstatushistory>();

            var order = orderRepo.GetAll().FirstOrDefault(o => o.Orderid == orderId);
            if (order == null) { _logger.LogWarning("Order {id} not found", orderId); return; }

            if (order.Currentstatus != OrderStatusEnum.DELIVERED.ToString()) return; // đã xử lý

            order.Currentstatus = OrderStatusEnum.COMPLETED.ToString();
            await orderRepo.UpdateAsync(order, false);

            await historyRepo.InsertAsync(new Orderstatushistory
            {
                Statushistoryid = Guid.NewGuid(),
                Orderid = orderId,
                Status = OrderStatusEnum.COMPLETED.ToString(),
                Statusdescription = "Tự động hoàn tất sau 48 giờ",
                Createdat = DateTime.UtcNow
            }, false);

            await _uow.SaveChangesAsync();
            _logger.LogInformation("Order {id} auto-completed.", orderId);
        }
    }
}
