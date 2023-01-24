using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EPiServer.Commerce.Order;
using EPiServer.Logging.Compatibility;
using EPiServer.ServiceLocation;
using Newtonsoft.Json;
using Foundation.Infrastructure.Commerce;
using Foundation.Infrastructure.Commerce.Customer;
using Foundation.Infrastructure.Commerce.Customer.Services;
using Foundation.Features.Checkout.Services;
using Foundation.Infrastructure;
using Foundation.Infrastructure.Cms;
using Foundation.Features.Stores;
using Microsoft.AspNetCore.Http;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    /// <summary>
    /// contains Cashfree API integration related methods
    /// </summary>
    public interface ICashfreeClient
    {
        Task<dynamic> CreateCashfree(IOrderGroup orderGroup, IPayment payment);
        Task<ResponseModel> GetOrderDetails(string order_id, string order_token);
        Task<ResponseModel> GetCashfreePaymentDetails(string order_id);
    }

    [ServiceConfiguration(typeof(ICashfreeClient))]//, Lifecycle = ServiceInstanceScope.HttpContext]
    public class CashfreeClient : ICashfreeClient
    {
        private readonly string _baseUrl;
        private readonly string _appID;
        private readonly string _secretKey;
        private readonly string _activeEnvironment;
        private readonly string _apiVersion;

        private static Injected<ICustomerService> _customerService;
        private static Injected<IOrdersService> _ordersService;
        private static Injected<IOrderRepository> _orderRepository;
        private readonly IStoreService _storeService;

        public CashfreeClient(IStoreService storeService)
        {
            var configuration = new CashfreeConfiguration();
            _baseUrl = configuration.CashfreeApiUrl;
            _appID = configuration.AppID;
            _secretKey = configuration.SecretKey;
            _activeEnvironment = configuration.ActiveEnvironment;
            _apiVersion = configuration.ApiVersion;

            _storeService = storeService;

        }
        
        public async Task<dynamic> CreateCashfree(IOrderGroup orderGroup, IPayment payment)
        {

            //string AppID = Settings.ContainsKey("AppID") ? Settings["AppID"] : null;
            //string SecretKey = Settings.ContainsKey("SecretKey") ? Settings["SecretKey"] : null;
            //string ActiveEnvt = Settings.ContainsKey("ActiveEnviroment") ? Settings["ActiveEnviroment"] : null;
            //string ApiVersion = Settings.ContainsKey("ApiVersion") ? Settings["ApiVersion"] : null;
            var httpContextAccessor = ServiceLocator.Current.GetInstance<IHttpContextAccessor>();
            var Request = httpContextAccessor.HttpContext?.Request;
            var uriBuilder = new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port ?? -1);
            if (uriBuilder.Uri.IsDefaultPort)
            {
                uriBuilder.Port = -1;
            }
            var storeLocation = uriBuilder.Uri.AbsoluteUri;

            //var storeLocation = "https://localhost:44397/";

            if (orderGroup == null)
            {
                return PaymentProcessingResult.CreateUnsuccessfulResult("Failed to process your payment.");
            }

            var currentOrder = orderGroup;
            var customer = _customerService.Service.GetContactViewModelById(currentOrder.CustomerId.ToString());
          
            if (customer == null)
            {
                return PaymentProcessingResult.CreateUnsuccessfulResult("Failed to process your payment.");
            }


            HttpRequestMessage request;
            HttpResponseMessage response;
            string responsebody;
            var client = new HttpClient();
            var url = _activeEnvironment == "Test" ?
                "https://sandbox.cashfree.com/pg/orders" :
                "https://api.cashfree.com/pg/orders";
            var cur = orderGroup.Currency.ToString();
            request = new HttpRequestMessage(HttpMethod.Post, url);
            var stringdata = JsonConvert.SerializeObject(new CashfreeModel()
            {
                order_id = ("EPI_FOUNDATION5"+orderGroup.OrderLink.OrderGroupId).Trim(),//EPI_FOUNDATION4
                order_amount = Convert.ToDouble(payment.Amount),
                order_currency = orderGroup.Currency.ToString(),//"INR",
                order_note = "order:" + payment.PaymentId,
                customer_details = new CustomerDetails()
                {
                    customer_id = orderGroup.CustomerId.ToString(),
                    customer_name = customer.FullName.ToString(),
                    customer_email = customer.Email.ToString(),
                    customer_phone = payment.BillingAddress.DaytimePhoneNumber.ToString()
                },
                order_meta = new OrderMeta()
                {
                    return_url = $"{storeLocation}Cashfree/HandleResponse?order_id={{order_id}}&order_token={{order_token}}",
                    notify_url = $"{storeLocation}Cashfree/NotifyUrl"
                    // payment_methods = _cashfreePaymentSettings.PaymentMethods
                }
            });

            var stringcontent = new StringContent(stringdata, Encoding.UTF8, "application/json");
            request.Content = stringcontent;
            var listheaders = new List<NameValueHeaderValue>
                {
                    new NameValueHeaderValue("x-api-version", _apiVersion),
                    new NameValueHeaderValue("x-client-id", _appID),
                    new NameValueHeaderValue("x-client-secret", _secretKey)
                };
            foreach (var header in listheaders)
            {
                request.Headers.Add(header.Name, header.Value);
            }

            response = await client.SendAsync(request);
            responsebody = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject<ResponseModel>(responsebody);

            if (response.ReasonPhrase != "OK")
            {
                return PaymentProcessingResult.CreateUnsuccessfulResult("Order is not Created");
            }
            var payment_link = result.payment_link;

            return payment_link;
        }

        public async Task<ResponseModel> GetOrderDetails(string order_id, string order_token)
        {

            var client = new HttpClient();
            var url = _activeEnvironment == "Test" ?
                new Uri("https://sandbox.cashfree.com/pg/orders/" + order_id) :
               new Uri("https://api.cashfree.com/pg/orders/" + order_id);

            client.DefaultRequestHeaders.Add("x-api-version", _apiVersion);
            client.DefaultRequestHeaders.Add("x-client-id", _appID);
            client.DefaultRequestHeaders.Add("x-client-secret", _secretKey);

            //get the order details from cashfree
            var result = await client.GetAsync(url);
            if (result.IsSuccessStatusCode != true)
            {
                return null;
            }
            var json = result.Content.ReadAsStringAsync().Result;
            dynamic result2 = JsonConvert.DeserializeObject<ResponseModel>(json);

            var responseModel = new ResponseModel();
            responseModel.order_status = result2.order_status;//The order status -ACTIVE, PAID, EXPIRED
            responseModel.customer_id = result2.customer_details.customer_id;

            return responseModel;
        }
        public async Task<ResponseModel> GetCashfreePaymentDetails(string order_id)
        {
            var client = new HttpClient();
            var url = _activeEnvironment == "Test" ?
                new Uri("https://sandbox.cashfree.com/pg/orders/" + order_id + "/payments") :
               new Uri("https://api.cashfree.com/pg/orders/" + order_id + "/payments");
            client.DefaultRequestHeaders.Add("x-api-version", _apiVersion);
            client.DefaultRequestHeaders.Add("x-client-id", _appID);
            client.DefaultRequestHeaders.Add("x-client-secret", _secretKey);

            //get the payment details from cashfree
            var result = await client.GetAsync(url);
            if (result.IsSuccessStatusCode != true)
            {
                return null;
            }
            var json1 = result.Content.ReadAsStringAsync().Result;
            dynamic result2 = JsonConvert.DeserializeObject(json1);
            ResponseModel responseModel = new ResponseModel();
            foreach (var i in result2)
            {
                if (i.payment_status != null)
                {
                    responseModel.is_captured = i.is_captured;
                    responseModel.payment_status = i.payment_status;
                }

            }
            return responseModel;
        }

    }
}
