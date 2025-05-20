using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class RewardHistoryResponse
    {
        public Guid Rewardhistoryid { get; set; }

        public string? Orderid { get; set; }

        public string? Ordername { get; set; }

        public int Points { get; set; }

        public DateTime? Datecreated { get; set; }
    }
}
