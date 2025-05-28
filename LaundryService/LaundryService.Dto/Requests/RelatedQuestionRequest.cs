using System;

namespace LaundryService.Dto.Requests;

public class RelatedQuestionsRequest
{
  /// <summary>
  /// Chủ đề hoặc từ khóa để lấy câu hỏi liên quan
  /// </summary>
  public string Topic { get; set; }

  /// <summary>
  /// Số lượng câu hỏi muốn lấy
  /// </summary>
  public int? Count { get; set; }
}
