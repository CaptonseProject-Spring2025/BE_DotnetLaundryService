using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Requests
{
    public class CreateRatingRequest
    {
        [Required]
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
        public int Star { get; set; }

        [MaxLength(150, ErrorMessage = "Đánh giá không được vượt quá 150 ký tự.")]
        public string Review { get; set; }
    }

}
