using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class DriverCashDailyResponse
    {
        public Guid DriverId { get; set; }
        public string DriverName { get; set; } = null!;
        public string? DriverAvatar { get; set; }
        public string? DriverPhone { get; set; }

        public int CashOrdersCount { get; set; }   // Số đơn trả tiền mặt
        public decimal TotalCollectedAmount { get; set; }   // Tổng tiền đã thu
        public int ReturnedOrdersCount { get; set; }    // Tổng số đơn đã trả tiền cho admin
        public decimal TotalReturnedAmount { get; set; }   // Tổng số tiền dã nộp về admin
        public decimal TotalUnreturnedAmount { get; set; }   //Tổng số tiền chưa nộp về admin
    }

}
