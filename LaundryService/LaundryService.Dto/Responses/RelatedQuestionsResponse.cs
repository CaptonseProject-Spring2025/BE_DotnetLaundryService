using System;

namespace LaundryService.Dto.Responses;

public class RelatedQuestionsResponse
{
   /// <summary>
        /// Trạng thái xử lý
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Danh sách các câu hỏi liên quan
        /// </summary>
        public List<string> Questions { get; set; }
        
        /// <summary>
        /// Thông báo lỗi (nếu có)
        /// </summary>
        public string Error { get; set; }
}
