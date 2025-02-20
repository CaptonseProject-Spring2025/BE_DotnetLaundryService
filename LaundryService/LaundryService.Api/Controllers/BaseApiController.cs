using LaundryService.Domain.Constants;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaseApiController : ControllerBase
    {
        public BaseApiController() { }
        public async Task<ResponseDataDTO<T>> HandleException<T>(Task<T> task)
        {
            try
            {
                var data = await task;
                return new ResponseDataDTO<T> { Success = true, Data = data };
            }
            catch (ApplicationException ex)
            {
                return new ResponseDataDTO<T> { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, ex.Message);
                return new ResponseDataDTO<T> { Success = false, Message = MessageConstants.CommonMessage.ERROR_HAPPENED };
            }
        }
    }
}
