using LaundryService.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/mapbox")]
    [ApiController]
    public class MapboxController : ControllerBase
    {
        private readonly IMapboxService _mapboxService;

        public MapboxController(IMapboxService mapboxService)
        {
            _mapboxService = mapboxService;
        }

        [HttpGet("geocoding")]
        public async Task<IActionResult> GetCoordinates([FromQuery] string address)
        {
            var (latitude, longitude) = await _mapboxService.GetCoordinatesFromAddressAsync(address);
            return Ok(new { Latitude = latitude, Longitude = longitude });
        }

        [HttpGet("calculate-distance")]
        public IActionResult CalculateDistance([FromQuery] decimal lat1, [FromQuery] decimal lon1, [FromQuery] decimal lat2, [FromQuery] decimal lon2)
        {
            double distance = _mapboxService.CalculateDistance(lat1, lon1, lat2, lon2);
            return Ok(new { Distance = distance });
        }
    }
}
