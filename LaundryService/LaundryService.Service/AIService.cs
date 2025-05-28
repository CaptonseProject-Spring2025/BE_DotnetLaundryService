using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using Newtonsoft.Json;

namespace LaundryService.Service
{
  public class AIService : IAIService
  {
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey = "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";
    private readonly string _apiUrl = "https://openrouter.ai/api/v1/chat/completions";

    // Cache cho context
    private string _laundryServiceContext;

    public AIService(IUnitOfWork unitOfWork, IHttpClientFactory httpClientFactory)
    {
      _unitOfWork = unitOfWork;
      _httpClientFactory = httpClientFactory;

      // Khởi tạo context khi service được tạo
      UpdateKnowledgeBaseAsync().Wait();
    }

    public async Task<string> GetResponseAsync(string query, string language = "vi")
    {
      var client = _httpClientFactory.CreateClient();
      client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

      // Tạo system prompt kết hợp với context về dịch vụ giặt là và yêu cầu trả lời bằng tiếng Việt
      var systemPrompt = $@"Bạn là trợ lý AI của dịch vụ giặt là. Hãy trả lời câu hỏi dựa vào thông tin về dịch vụ giặt là sau đây:
            
{_laundryServiceContext}

Luôn trả lời bằng tiếng Việt, không quan trọng người dùng hỏi bằng ngôn ngữ nào.
Chỉ trả lời dựa trên thông tin được cung cấp. Nếu không có thông tin liên quan, hãy nói 'Tôi không có thông tin về vấn đề này trong hệ thống dịch vụ giặt là.'";

      var requestBody = new
      {
        model = "deepseek/deepseek-r1-zero:free",
        messages = new[]
          {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = query }
                }
      };

      var jsonContent = JsonConvert.SerializeObject(requestBody);
      var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

      var response = await client.PostAsync(_apiUrl, content);

      if (response.IsSuccessStatusCode)
      {
        var jsonResponse = await response.Content.ReadAsStringAsync();
        dynamic parsedResponse = JsonConvert.DeserializeObject(jsonResponse);
        return parsedResponse.choices[0].message.content.ToString();
      }

      throw new Exception($"Error calling AI API: {await response.Content.ReadAsStringAsync()}");
    }
    public async Task UpdateKnowledgeBaseAsync()
    {
      var stringBuilder = new StringBuilder();

      // Lấy thông tin từ các bảng chính
      await AddServiceInfo(stringBuilder);
      await AddPricingInfo(stringBuilder);
      await AddOrderProcessInfo(stringBuilder);
      await AddFAQInfo(stringBuilder);

      _laundryServiceContext = stringBuilder.ToString();
    }

    private async Task AddServiceInfo(StringBuilder sb)
    {
      // Lấy thông tin về dịch vụ
      var categories = await _unitOfWork.Repository<Servicecategory>().GetAllAsync();
      var subservices = await _unitOfWork.Repository<Subservice>().GetAllAsync();
      var serviceDetails = await _unitOfWork.Repository<Servicedetail>().GetAllAsync();

      sb.AppendLine("### THÔNG TIN DỊCH VỤ GIẶT LÀ:");
      sb.AppendLine("Các loại dịch vụ:");

      foreach (var category in categories)
      {
        sb.AppendLine($"- {category.Name}:");

        var categorySubservices = subservices.Where(s => s.Categoryid == category.Categoryid).ToList();
        foreach (var subservice in categorySubservices)
        {
          sb.AppendLine($"  * {subservice.Name}:");

          var services = serviceDetails.Where(sd => sd.Subserviceid == subservice.Subserviceid).ToList();
          foreach (var service in services)
          {
            sb.AppendLine($"    + {service.Name}: {service.Price} VNĐ - {service.Description}");
          }
        }
      }

      sb.AppendLine();
    }

    private async Task AddPricingInfo(StringBuilder sb)
    {
      // Thêm thông tin về giá cả và chính sách giá
      var extras = await _unitOfWork.Repository<Extra>().GetAllAsync();

      sb.AppendLine("### THÔNG TIN GIÁ CẢ VÀ PHỤ PHÍ:");
      sb.AppendLine("Các loại phụ phí:");

      foreach (var extra in extras)
      {
        sb.AppendLine($"- {extra.Name}: {extra.Price} VNĐ - {extra.Description}");
      }

      sb.AppendLine();
    }

    private async Task AddOrderProcessInfo(StringBuilder sb)
    {
      sb.AppendLine("### QUY TRÌNH ĐƠN HÀNG:");
      sb.AppendLine("1. Khách hàng tạo đơn hàng (INCART)");
      sb.AppendLine("2. Đặt hàng (PENDING)");
      sb.AppendLine("3. Xác nhận đơn hàng (CONFIRMED)");
      sb.AppendLine("4. Tài xế đến lấy hàng (PICKUP)");
      sb.AppendLine("5. Hàng đã được lấy (PICKEDUP)");
      sb.AppendLine("6. Nhân viên kiểm tra (CHECKING)");
      sb.AppendLine("7. Đã kiểm tra xong (CHECKED)");
      sb.AppendLine("8. Đang giặt (WASHING)");
      sb.AppendLine("9. Đã giặt xong (WASHED)");
      sb.AppendLine("10. Kiểm tra chất lượng (QUALITY_CHECKED)");
      sb.AppendLine("11. Lên lịch giao hàng (SCHEDULED_DELIVERY)");
      sb.AppendLine("12. Đang giao hàng (DELIVERING)");
      sb.AppendLine("13. Đã giao hàng (DELIVERED)");
      sb.AppendLine("14. Hoàn thành (COMPLETED)");

      sb.AppendLine();
    }

    private async Task AddFAQInfo(StringBuilder sb)
    {
      sb.AppendLine("### CÂU HỎI THƯỜNG GẶP:");
      sb.AppendLine("Q: Tôi cần làm gì để đặt dịch vụ giặt là?");
      sb.AppendLine("A: Bạn cần đăng ký tài khoản, chọn dịch vụ giặt là phù hợp, thêm vào giỏ hàng và tiến hành đặt hàng.");

      sb.AppendLine("Q: Làm thế nào để thanh toán?");
      sb.AppendLine("A: Chúng tôi chấp nhận thanh toán tiền mặt khi giao hàng hoặc thanh toán qua PayOS.");

      sb.AppendLine("Q: Làm thế nào để theo dõi đơn hàng?");
      sb.AppendLine("A: Bạn có thể theo dõi đơn hàng qua mục 'Đơn hàng của tôi' sau khi đăng nhập.");

      sb.AppendLine();
    }
    /// <summary>
    /// Lấy danh sách câu hỏi liên quan đến chủ đề
    /// </summary>
    public async Task<List<string>> GetRelatedQuestionsAsync(string topic = null, int count = 5)
    {
      // Nếu không có topic, lấy câu hỏi phổ biến
      if (string.IsNullOrEmpty(topic))
      {
        return await GetPopularQuestionsAsync(count);
      }

      var client = _httpClientFactory.CreateClient();
      client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

      // System prompt để yêu cầu AI tạo các câu hỏi liên quan
      var systemPrompt = $@"Bạn là một AI chuyên gia về dịch vụ giặt là. Hãy tạo ra {count} câu hỏi có thể đặt cho chatbot về dịch vụ giặt là liên quan đến chủ đề: ""{topic}"".

Các câu hỏi phải:
- Liên quan trực tiếp đến chủ đề và dịch vụ giặt là
- Viết bằng tiếng Việt
- Ngắn gọn, súc tích và có ý nghĩa
- Đa dạng và hữu ích
- Không lặp lại
- Là câu hỏi thực tế mà khách hàng có thể hỏi

Trả lời chỉ với danh sách các câu hỏi, mỗi câu một dòng, không có số thứ tự hay ký hiệu đầu dòng.";

      var requestBody = new
      {
        model = "deepseek/deepseek-r1-zero:free",
        messages = new[]
          {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Tạo {count} câu hỏi liên quan đến {topic} trong lĩnh vực giặt là" }
                }
      };

      var jsonContent = JsonConvert.SerializeObject(requestBody);
      var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

      var response = await client.PostAsync(_apiUrl, content);

      if (response.IsSuccessStatusCode)
      {
        var jsonResponse = await response.Content.ReadAsStringAsync();
        dynamic parsedResponse = JsonConvert.DeserializeObject(jsonResponse);
        string generatedText = parsedResponse.choices[0].message.content.ToString();

        // Parse các câu hỏi từ text trả về
        return ParseQuestionsFromText(generatedText, count);
      }

      throw new Exception($"Error calling AI API: {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Lấy danh sách các câu hỏi phổ biến về dịch vụ giặt là
    /// </summary>
    private async Task<List<string>> GetPopularQuestionsAsync(int count)
    {
      // Danh sách các câu hỏi phổ biến được chuẩn bị trước
      var popularQuestions = new List<string>
            {
                "Các loại dịch vụ giặt là nào có sẵn?",
                "Làm thế nào để tính phí vận chuyển?",
                "Thời gian hoàn thành dịch vụ nhanh nhất là bao lâu?",
                "Có thể đặt dịch vụ giặt khẩn cấp không?",
                "Cách thức thanh toán được chấp nhận?",
                "Làm thế nào để theo dõi đơn hàng?",
                "Quy trình xử lý đối với đồ dễ hỏng như thế nào?",
                "Có bảo hành cho dịch vụ giặt không?",
                "Chính sách hoàn tiền và khiếu nại?",
                "Có giảm giá cho khách hàng thường xuyên không?",
                "Quy trình giặt có thân thiện với môi trường không?",
                "Làm thế nào để hủy đơn hàng?",
                "Có thể chọn thời gian giao nhận cụ thể không?",
                "Chi phí giặt các loại quần áo đặc biệt?",
                "Có dịch vụ giặt rèm cửa/chăn ga không?",
                "Làm thế nào để đổi ngày nhận hàng?",
                "Trường hợp khẩn cấp có thể liên hệ ai?",
                "Làm thế nào để báo cáo vấn đề với đơn hàng?",
                "Các khu vực nào được phục vụ giao hàng?",
                "Có thêm tiền phụ thu vào cuối tuần không?"
            };

      // Random lấy count câu hỏi từ danh sách
      Random random = new Random();
      List<string> selectedQuestions = new List<string>();

      int questionsCount = Math.Min(count, popularQuestions.Count);

      for (int i = 0; i < questionsCount; i++)
      {
        int index = random.Next(popularQuestions.Count);
        selectedQuestions.Add(popularQuestions[index]);
        popularQuestions.RemoveAt(index);
      }

      return selectedQuestions;
    }

    /// <summary>
    /// Parse các câu hỏi từ text trả về từ AI
    /// </summary>
    private List<string> ParseQuestionsFromText(string text, int count)
    {
      List<string> questions = new List<string>();

      // Split text theo dòng mới
      var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

      foreach (var line in lines)
      {
        var trimmedLine = line.Trim();

        // Bỏ qua các dòng không phải câu hỏi
        if (string.IsNullOrWhiteSpace(trimmedLine))
          continue;

        // Loại bỏ số thứ tự hoặc dấu đầu dòng nếu có
        var question = trimmedLine;
        if (trimmedLine.StartsWith("- "))
        {
          question = trimmedLine.Substring(2);
        }
        else if (trimmedLine.StartsWith("* "))
        {
          question = trimmedLine.Substring(2);
        }
        else if (trimmedLine.Length >= 3 && char.IsDigit(trimmedLine[0]) && trimmedLine[1] == '.' && trimmedLine[2] == ' ')
        {
          question = trimmedLine.Substring(3);
        }

        questions.Add(question);

        // Nếu đã đủ số lượng câu hỏi, dừng lại
        if (questions.Count >= count)
          break;
      }

      return questions;
    }

  }
}