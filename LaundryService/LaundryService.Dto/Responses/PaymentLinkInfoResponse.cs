using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class PaymentLinkInfoResponse
    {
        public string Id { get; set; } = null!;
        public long OrderCode { get; set; }
        public int Amount { get; set; }
        public int AmountPaid { get; set; }
        public int AmountRemaining { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CanceledAt { get; set; }

        public List<PaymentLinkTransactionResponse> Transactions { get; set; } = new();
    }

    public class PaymentLinkTransactionResponse
    {
        public string Reference { get; set; } = null!;
        public int Amount { get; set; }
        public string AccountNumber { get; set; }
        public string Description { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string? CounterAccountBankId { get; set; }
        public string? CounterAccountBankName { get; set; }
        public string? CounterAccountName { get; set; }
        public string? CounterAccountNumber { get; set; }
        public string? VirtualAccountName { get; set; }
        public string? VirtualAccountNumber { get; set; }
    }
}
