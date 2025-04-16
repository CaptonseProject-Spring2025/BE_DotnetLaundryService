using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Net.payOS.Types;
using Net.payOS;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IPaymentService
    {
        Task<PaymentMethodResponse> CreatePaymentMethodAsync(CreatePaymentMethodRequest request);

        Task<List<PaymentMethodResponse>> GetAllPaymentMethodsAsync();

        Task<CreatePayOSPaymentLinkResponse> CreatePayOSPaymentLinkAsync(CreatePayOSPaymentLinkRequest request);

        Task<PaymentLinkInfoResponse> GetPayOSPaymentLinkInfoAsync(Guid paymentId);

        Task<string> ConfirmPayOSCallbackAsync(string paymentLinkId, string status);

        /// <summary>
        /// Handles incoming webhook notifications from PayOS.
        /// Verifies the data and updates payment and order status accordingly.
        /// </summary>
        /// <param name="webhookBody">The raw webhook payload received from PayOS.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        Task HandlePayOSWebhookAsync(WebhookType webhookBody);
    }
}
