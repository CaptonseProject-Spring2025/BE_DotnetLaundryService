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

        [HttpGet("get-order-statistics")]
        public async Task<ActionResult> GetOrderStatistics()
        {
            var statistics = await _dashBoardServices.GetOrderStatisticAsync();
            return Ok(statistics);
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

    }
}