using System;

namespace LaundryService.Domain.Interfaces.Services;

public interface IAIService
{
  /// <summary>
  /// Xử lý câu hỏi và trả về câu trả lời bằng tiếng Việt dựa trên dữ liệu của hệ thống
  /// </summary>
  /// <param name="query">Câu hỏi của người dùng</param>
  /// <param name="language">Ngôn ngữ trả lời (mặc định: "vi" cho tiếng Việt)</param>
  /// <returns>Câu trả lời bằng tiếng Việt dựa trên context về dịch vụ giặt là</returns>
  Task<string> GetResponseAsync(string query, string language = "vi");

  /// <summary>
  /// Cập nhật cơ sở kiến thức cho AI
  /// </summary>
  Task UpdateKnowledgeBaseAsync();

  /// <summary>
  /// Lấy danh sách các câu hỏi gợi ý liên quan đến nội dung
  /// </summary>
  /// <param name="topic">Chủ đề hoặc từ khóa (tùy chọn)</param>
  /// <param name="count">Số lượng câu hỏi muốn lấy (mặc định: 5)</param>
  /// <returns>Danh sách các câu hỏi gợi ý</returns>
  Task<List<string>> GetRelatedQuestionsAsync(string topic = null, int count = 5);
}