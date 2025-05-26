using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using LaundryService.Dto.Responses;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IDashBoardServices
    {
        public Task<int> GetUserCountAsync();

        public Task<int> GetAllOrdersByCurrentStatusAsync();



        public Task<int> GetAllServicesAsync();


        public Task<int> GetAllExtrasAsync();


        public Task<Object> GetOrderStatisticAsync();

        public Task<Object> GetCustomerStatistic();


        //Các phương thức tính tổng doanh thu
        /// <summary>
        /// Lấy tổng doanh thu với trạng thái thanh toán là PAID
        /// </summary>
        /// <returns>Tổng doanh thu</returns>
        public Task<decimal> GetTotalRevenueAsync();

        /// <summary>
        /// Lấy doanh thu theo ngày với trạng thái thanh toán là PAID
        /// </summary>
        /// <param name="date">Ngày cần thống kê (mặc định là ngày hiện tại)</param>
        /// <returns>Doanh thu theo ngày</returns>
        public Task<decimal> GetDailyRevenueAsync(DateTime? date = null);


        /// <summary>
        /// Lấy doanh thu theo tháng với trạng thái thanh toán là PAID
        /// </summary>
        /// <param name="month">Tháng cần thống kê (1-12)</param>
        /// <param name="year">Năm cần thống kê (mặc định là năm hiện tại)</param>
        /// <returns>Doanh thu theo tháng</returns>
        public Task<decimal> GetMonthlyRevenueAsync(int month, int? year = null);



        /// <summary>
        /// Lấy doanh thu theo năm với trạng thái thanh toán là PAID
        /// </summary>
        /// <param name="year">Năm cần thống kê (mặc định là năm hiện tại)</param>
        /// <returns>Doanh thu theo năm</returns>
        public Task<decimal> GetYearlyRevenueAsync(int? year = null);


        /// <summary>
        /// Lấy doanh thu theo phương thức thanh toán với trạng thái thanh toán là PAID
        /// </summary>
        /// <param name="paymentMethodId">ID của phương thức thanh toán (PayOS hoặc Cash)</param>
        /// <returns>Doanh thu và tên phương thức thanh toán</returns>
        public Task<(decimal Revenue, string Name)> GetRevenueByPaymentMethodAsync(Guid paymentMethodId);


        /// <summary>
        /// Lấy chi tiết doanh thu theo khoảng thời gian và phương thức thanh toán
        /// </summary>
        /// <param name="startDate">Ngày bắt đầu</param>
        /// <param name="endDate">Ngày kết thúc</param>
        /// <param name="paymentMethodId">ID của phương thức thanh toán (tùy chọn)</param>
        /// <returns>Chi tiết doanh thu</returns>
        public Task<RevenueDetailStatistic> GetRevenueDetailAsync(DateTime startDate, DateTime endDate, Guid? paymentMethodId = null);

        /// <summary>
        /// Lấy thống kê doanh thu theo thời gian (ngày, tuần, tháng, năm)
        /// </summary>
        /// <param name="timeFrame">Khoảng thời gian: "day", "week", "month", "year"</param>
        /// <returns>Thống kê doanh thu theo thời gian</returns>
        public Task<RevenueTimeStatistic> GetRevenueStatisticByTimeFrameAsync(string timeFrame);
    }
}