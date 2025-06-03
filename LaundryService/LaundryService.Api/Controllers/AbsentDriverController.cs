using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [ApiController]
    [Route("api/admin/driver-absents")]
    [Authorize(Roles = "Admin")]
    public class AbsentDriverController : ControllerBase
    {
        private readonly IAbsentDriverService _service;
        public AbsentDriverController(IAbsentDriverService service) => _service = service;

        /// <summary>
        /// Tạo mới lịch vắng cho tài xế.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AbsentDriverCreateRequest req)
        {
            try
            {
                var result = await _service.AddAbsentAsync(req);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật thông tin lịch vắng.
        /// </summary>
        [HttpPut("{absentId:guid}")]
        public async Task<IActionResult> Update(Guid absentId, [FromBody] AbsentDriverUpdateRequest req)
        {
            try
            {
                var result = await _service.UpdateAbsentAsync(absentId, req);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa lịch vắng của tài xế.
        /// </summary>
        [HttpDelete("{absentId:guid}")]
        public async Task<IActionResult> Delete(Guid absentId)
        {
            try
            {
                await _service.DeleteAbsentAsync(absentId);
                return Ok(new { Message = "Xóa lịch vắng thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả lịch vắng.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _service.GetAllAbsentsAsync();
                return Ok(list);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }

        }
    }
}