using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
//using LaundryService.Api.Models;

namespace LaundryService.Api.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class OpenAiController : ControllerBase
  {
    // API Key của bạn
    private const string apiKey = "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";
    private const string apiUrl = "https://openrouter.ai/api/v1/chat/completions"; // Địa chỉ API

    [HttpPost("get-ai-response")]
    public async Task<IActionResult> GetAIResponse([FromBody] string query)
    {
      // URL của OpenAI API
      var apiUrl = "https://openrouter.ai/api/v1/chat/completions";

      // API key
      var apiKey = "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";

      // Tạo HttpClient để gọi API
      using var client = new HttpClient();

      // Đặt Authorization Header
      client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

      // Cấu trúc của body JSON
      var requestBody = new
      {
        model = "deepseek/deepseek-r1-zero:free",
        messages = new[]
          {
            new
            {
                role = "user",
                content = query
            }
        }
      };

      // Chuyển body thành JSON
      var jsonContent = JsonConvert.SerializeObject(requestBody);

      // Đóng gói nội dung trong StringContent và thiết lập Content-Type
      var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

      // Gửi yêu cầu POST tới API
      HttpResponseMessage response = await client.PostAsync(apiUrl, content);

      // Kiểm tra xem yêu cầu có thành công không
      if (response.IsSuccessStatusCode)
      {
        var responseJson = await response.Content.ReadAsStringAsync();
        return Ok(responseJson);
      }
      else
      {
        var errorResponse = await response.Content.ReadAsStringAsync();
        return StatusCode(500, $"Error calling OpenAI API: {errorResponse}");
      }
    }
  }
}