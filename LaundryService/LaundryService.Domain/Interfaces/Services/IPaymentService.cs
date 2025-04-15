using LaundryService.Dto.Requests;
using LaundryService.Dto.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces.Services
{
    public interface IPaymentService
    {
        Task<PaymentMethodResponse> CreatePaymentMethodAsync(CreatePaymentMethodRequest request);

        Task<List<PaymentMethodResponse>> GetAllPaymentMethodsAsync();

        Task<CreatePayOSPaymentLinkResponse> CreatePayOSPaymentLinkAsync(CreatePayOSPaymentLinkRequest request);
    }
}
