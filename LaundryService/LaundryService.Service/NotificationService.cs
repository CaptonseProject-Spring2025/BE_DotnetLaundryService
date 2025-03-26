using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Domain.Entities;

namespace LaundryService.Service
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUtil _util;

        public NotificationService(IUnitOfWork unitOfWork, IUtil util)
        {
            _unitOfWork = unitOfWork;
            _util = util;
        }
        public async Task CreateOrderPlacedNotificationAsync(Guid userId, Guid orderId)
        {
            var notification = new Notification
            {
                Notificationid = Guid.NewGuid(),
                Userid = userId,
                Title = "Thông báo đặt hàng",
                Message = "Bạn đã đặt hàng thành công. Vui lòng chờ nhân viên liên hệ.",
                Notificationtype = "OrderPlaced",
                Orderid = orderId,
                Createdat = DateTime.UtcNow,
                Ispushenabled = true,
                Isread = false
            };

            await _unitOfWork.Repository<Notification>().InsertAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
