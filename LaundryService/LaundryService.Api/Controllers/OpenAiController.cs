using System;
using System.Threading.Tasks;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using Microsoft.AspNetCore.Mvc;

namespace LaundryService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OpenAiController : ControllerBase
    {
        private readonly IAIService _aiService;

        public OpenAiController(IAIService aiService)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// Nhận câu trả lời từ AI liên quan đến dữ liệu dịch vụ giặt là
        /// </summary>
        [HttpPost("get-ai-response")]
        public async Task<ActionResult<AIResponse>> GetAIResponse([FromBody] AIRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Query))
                {
                    return BadRequest(new AIResponse 
                    { 
                        Success = false, 
                        Error = "Truy vấn không được để trống" 
                    });
                }
                
                // Luôn truyền "vi" để đảm bảo trả lời bằng tiếng Việt
                var response = await _aiService.GetResponseAsync(request.Query, "vi");
                return Ok(new AIResponse 
                { 
                    Success = true, 
                    Response = response 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AIResponse 
                { 
                    Success = false, 
                    Error = ex.Message 
                });
            }
        }
        
        /// <summary>
        /// Cập nhật cơ sở kiến thức cho AI từ dữ liệu hiện tại
        /// </summary>
        [HttpPost("update-knowledge")]
        public async Task<ActionResult<AIResponse>> UpdateKnowledge()
        {
            try
            {
                await _aiService.UpdateKnowledgeBaseAsync();
                return Ok(new AIResponse 
                { 
                    Success = true, 
                    Response = "Knowledge base updated successfully" 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AIResponse 
                { 
                    Success = false, 
                    Error = ex.Message 
                });
            }
        }
        
        /// <summary>
        /// Lấy danh sách các câu hỏi liên quan đến chủ đề
        /// </summary>
        /// <param name="request">
        ///   <see cref="RelatedQuestionsRequest"/> chứa:
        ///   - <c>Topic</c>: Chủ đề hoặc từ khóa (tùy chọn)
        ///   - <c>Count</c>: Số lượng câu hỏi muốn lấy (mặc định: 5)
        /// </param>
        /// <returns>
        ///   Danh sách các câu hỏi gợi ý để người dùng có thể hỏi AI
        /// </returns>
        /// <remarks>
        /// Nếu không chỉ định Topic, hệ thống sẽ trả về các câu hỏi phổ biến.
        /// Nếu có chỉ định Topic, hệ thống sẽ tạo các câu hỏi liên quan đến chủ đề đó.
        /// </remarks>
        [HttpPost("related-questions")]
        public async Task<ActionResult<RelatedQuestionsResponse>> GetRelatedQuestions([FromBody] RelatedQuestionsRequest request)
        {
            try
            {
                // Mặc định là 5 câu hỏi nếu không chỉ định
                int count = request.Count ?? 5;
                
                // Giới hạn số lượng câu hỏi từ 1-10
                count = Math.Max(1, Math.Min(count, 10));
                
                var questions = await _aiService.GetRelatedQuestionsAsync(request.Topic, count);
                return Ok(new RelatedQuestionsResponse 
                { 
                    Success = true, 
                    Questions = questions 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new RelatedQuestionsResponse 
                { 
                    Success = false, 
                    Error = ex.Message 
                });
            }
        }
    }
}