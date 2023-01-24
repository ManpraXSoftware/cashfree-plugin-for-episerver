using EPiServer.Commerce.Order;
using EPiServer.ServiceLocation;
using Foundation.Features.Checkout.Services;
using Foundation.Features.Checkout.Payments.Cashfree;
using Foundation.Features.MyOrganization;
using Foundation.Features.MyOrganization.Budgeting;
using Foundation.Infrastructure;
using Foundation.Infrastructure.Commerce;
using Foundation.Infrastructure.Commerce.Customer;
using Foundation.Infrastructure.Commerce.Customer.Services;
using Mediachase.Commerce.Orders;
using Mediachase.Commerce.Plugins.Payment;
//using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
//using Microsoft.AspNetCore.Http;
using EPiServer.Logging.Compatibility;
using Mediachase.Commerce.Customers;
using Microsoft.AspNetCore.Http;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    public class CashfreePaymentGateway : AbstractPaymentGateway, IPaymentPlugin
    {

        private static Injected<IOrdersService> _ordersService;
        private static Injected<IOrderRepository> _orderRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICashfreeClient _cashfreeClient;

        public CashfreePaymentGateway()
            : this(ServiceLocator.Current.GetInstance<IHttpContextAccessor>(),
                  ServiceLocator.Current.GetInstance<ICashfreeClient>())
        { }


        public CashfreePaymentGateway(IHttpContextAccessor httpContextAccessor,
            ICashfreeClient cashfreeClient)
        {
            _httpContextAccessor = httpContextAccessor;
            _cashfreeClient = cashfreeClient;
           
        }



        public PaymentProcessingResult ProcessPayment(IOrderGroup orderGroup, IPayment payment)
        {

            //payment.Status = PaymentStatus.Processed.ToString();
            if (_httpContextAccessor == null)
            {
                return PaymentProcessingResult.CreateUnsuccessfulResult("ProcessPaymentNullHttpContext");
            }

            if (payment == null)
            {
                return PaymentProcessingResult.CreateUnsuccessfulResult("PaymentNotSpecified");
            }
            var message = string.Empty;
            var cashfree = _cashfreeClient.CreateCashfree(orderGroup, payment);
            if(cashfree == null)
            {
                return PaymentProcessingResult.CreateUnsuccessfulResult(message);
            }
           
            var redirectURL = cashfree.Result;
            message = $"---Cashfree CreatePreOrder is successful. Redirect end user to {redirectURL}";
            return PaymentProcessingResult.CreateSuccessfulResult(message,redirectURL);
        }

        public override bool ProcessPayment(Payment payment, ref string message)
        {
            var orderGroup = payment.Parent.Parent;
            var result = ProcessPayment(orderGroup, payment);

            if (!string.IsNullOrEmpty(result.RedirectUrl))
            {
                _httpContextAccessor.HttpContext.Response.Redirect(result.RedirectUrl);
            }
            message = result.Message;
            return result.IsSuccessful;
        }

    }
}