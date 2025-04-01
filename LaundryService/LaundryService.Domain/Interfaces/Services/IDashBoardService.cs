using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IDashBoardServices
    {
        public Task<int> GetUserCountAsync();

        public Task<int> GetAllOrdersByCurrentStatusAsync();


        public Task<Object> GetOrderStatisticAsync();    


        public Task<int> GetAllServicesAsync();


        public Task<int> GetAllExtrasAsync();
    
    }
}