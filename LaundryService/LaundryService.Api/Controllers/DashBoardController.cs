using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashBoardController : ControllerBase
    {
        private readonly IDashBoardServices _dashBoardServices;


        public DashBoardController(IDashBoardServices dashBoardServices)
        {
            _dashBoardServices = dashBoardServices;
        }
        [HttpGet("get-customers-number")]
        public async Task<ActionResult> GetUserCount()
        {
            //Lấy user có role là customer và nó đang active         
            int customersCount = _dashBoardServices.GetUserCountAsync().Result;

            return Ok(new { CustomersNumber = customersCount });
        }

        [HttpGet("get-all-orders-numbers")]

        public async Task<ActionResult> GetAllOrdersByCurrentStatus()
        {

            int ordersCount = _dashBoardServices.GetAllOrdersByCurrentStatusAsync().Result;

            return Ok(new { OrderNumbers = ordersCount });
        }



        [HttpGet("get-all-services-numbers")]
        public async Task<ActionResult> GetAllServices()
        {
            int servicesCount = _dashBoardServices.GetAllServicesAsync().Result;
            return Ok(new { ServicesNumbers = servicesCount });
        }

        [HttpGet("get-all-extras-numbers")]
        public async Task<ActionResult> GetAllExtras()
        {
            int extrasCount = _dashBoardServices.GetAllExtrasAsync().Result;
            return Ok(new { ExtrasNumbers = extrasCount });
        }


        [HttpGet("get-order-statistics")]
        public async Task<ActionResult> GetOrderStatistics()
        {
            var statistics = await _dashBoardServices.GetOrderStatisticAsync();
            return Ok(statistics);
        }


        [HttpGet("get-customer-statistics")]
        public async Task<ActionResult> GetCustomerStatistics()
        {
            var statistics = await _dashBoardServices.GetCustomerStatistic();
            return Ok(statistics);
        }


        /// <summary>
        /// Lấy tất cả phương thức thanh toán trong hệ thống
        /// </summary>
        /// <param name="activeOnly">Chỉ lấy các phương thức đang hoạt động</param>
        /// <returns>Danh sách các phương thức thanh toán</returns>
        [HttpGet("get-all-payment-methods")]
        public async Task<ActionResult> GetAllPaymentMethods([FromQuery] bool activeOnly = true)
        {
            var paymentMethods = await _dashBoardServices.GetAllPaymentMethodsAsync(activeOnly);
            return Ok(paymentMethods);
        }

        /// <summary>
        /// Lấy tổng doanh thu theo từng phương thức thanh toán
        /// </summary>
        /// <returns>Danh sách phương thức thanh toán kèm doanh thu tương ứng</returns>
        [HttpGet("get-revenue-by-all-payment-methods")]
        public async Task<ActionResult> GetRevenueByAllPaymentMethods()
        {
            var revenueByMethods = await _dashBoardServices.GetRevenueByAllPaymentMethodsAsync();
            return Ok(revenueByMethods);
        }

        /// <summary>
        /// Lấy tổng doanh thu
        /// </summary>
        /// <returns>Tổng doanh thu với trạng thái thanh toán là PAID</returns>
        [HttpGet("get-total-revenue")]
        public async Task<ActionResult> GetTotalRevenue()
        {
            decimal totalRevenue = await _dashBoardServices.GetTotalRevenueAsync();
            return Ok(new { TotalRevenue = totalRevenue });
        }

        /// <summary>
        /// Lấy doanh thu theo ngày
        /// </summary>
        /// <param name="date">Ngày cần thống kê (định dạng: yyyy-MM-dd)</param>
        /// <returns>Doanh thu của ngày đó với trạng thái thanh toán là PAID</returns>
        [HttpGet("get-daily-revenue")]
        public async Task<ActionResult> GetDailyRevenue([FromQuery] DateTime? date = null)
        {
            decimal dailyRevenue = await _dashBoardServices.GetDailyRevenueAsync(date);
            return Ok(new { Date = date?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"), Revenue = dailyRevenue });
        }

        /// <summary>
        /// Lấy doanh thu theo tháng
        /// </summary>
        /// <param name="month">Tháng cần thống kê (1-12)</param>
        /// <param name="year">Năm cần thống kê</param>
        /// <returns>Doanh thu của tháng đó với trạng thái thanh toán là PAID</returns>
        [HttpGet("get-monthly-revenue")]
        public async Task<ActionResult> GetMonthlyRevenue([FromQuery] int month, [FromQuery] int? year = null)
        {
            // Kiểm tra tính hợp lệ của tháng
            if (month < 1 || month > 12)
            {
                return BadRequest(new { Message = "Tháng phải nằm trong khoảng từ 1 đến 12" });
            }

            year ??= DateTime.Today.Year;

            decimal monthlyRevenue = await _dashBoardServices.GetMonthlyRevenueAsync(month, year);
            return Ok(new { Month = month, Year = year, Revenue = monthlyRevenue });
        }

        /// <summary>
        /// Lấy doanh thu theo năm
        /// </summary>
        /// <param name="year">Năm cần thống kê</param>
        /// <returns>Doanh thu của năm đó với trạng thái thanh toán là PAID</returns>
        [HttpGet("get-yearly-revenue")]
        public async Task<ActionResult> GetYearlyRevenue([FromQuery] int? year = null)
        {
            year ??= DateTime.Today.Year;

            decimal yearlyRevenue = await _dashBoardServices.GetYearlyRevenueAsync(year);
            return Ok(new { Year = year, Revenue = yearlyRevenue });
        }

        /// <summary>
        /// Lấy doanh thu theo phương thức thanh toán
        /// </summary>
        /// <param name="paymentMethodId">ID của phương thức thanh toán</param>
        /// <returns>Doanh thu theo phương thức thanh toán với trạng thái thanh toán là PAID</returns>
        [HttpGet("get-revenue-by-payment-method/{paymentMethodId}")]
        public async Task<ActionResult> GetRevenueByPaymentMethod(Guid paymentMethodId)
        {
            var result = await _dashBoardServices.GetRevenueByPaymentMethodAsync(paymentMethodId);
            return Ok(new
            {
                PaymentMethodId = paymentMethodId,
                PaymentMethodName = result.Name,
                Revenue = result.Revenue
            });
        }

        /// <summary>
        /// Lấy chi tiết doanh thu theo khoảng thời gian và phương thức thanh toán
        /// </summary>
        /// <param name="startDate">Ngày bắt đầu (định dạng: yyyy-MM-dd)</param>
        /// <param name="endDate">Ngày kết thúc (định dạng: yyyy-MM-dd)</param>
        /// <param name="paymentMethodId">ID của phương thức thanh toán (tùy chọn)</param>
        /// <returns>Chi tiết doanh thu</returns>
        [HttpGet("get-revenue-detail")]
        public async Task<ActionResult> GetRevenueDetail(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] Guid? paymentMethodId = null)
        {
            // Kiểm tra tính hợp lệ của khoảng thời gian
            if (startDate > endDate)
            {
                return BadRequest(new { Message = "Ngày bắt đầu không thể sau ngày kết thúc" });
            }

            var revenueDetail = await _dashBoardServices.GetRevenueDetailAsync(startDate, endDate, paymentMethodId);
            return Ok(revenueDetail);
        }

        /// <summary>
        /// Lấy thống kê doanh thu theo thời gian
        /// </summary>
        /// <param name="timeFrame">Khoảng thời gian: "day", "week", "month", "year"</param>
        /// <returns>Thống kê doanh thu theo thời gian</returns>
        [HttpGet("get-revenue-statistic-by-timeframe")]
        public async Task<ActionResult> GetRevenueStatisticByTimeFrame([FromQuery] string timeFrame = "day")
        {
            // Kiểm tra tính hợp lệ của timeFrame
            if (!new[] { "day", "week", "month", "year" }.Contains(timeFrame.ToLower()))
            {
                return BadRequest(new { Message = "Khoảng thời gian không hợp lệ. Sử dụng một trong các giá trị: day, week, month, year" });
            }

            try
            {
                var revenueStatistic = await _dashBoardServices.GetRevenueStatisticByTimeFrameAsync(timeFrame);
                return Ok(revenueStatistic);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Đã xảy ra lỗi khi lấy thống kê doanh thu", Error = ex.Message });
            }
        }


    }
}