using EPiServer;
using EPiServer.Commerce.Order;
using EPiServer.Core;
using EPiServer.Editor;
using EPiServer.Web.Mvc;
using EPiServer.Web.Routing;
using Foundation.Features.MyOrganization.Organization;
using Foundation.Infrastructure.Commerce;
using Foundation.Infrastructure.Commerce.Customer.Services;
using Mediachase.Commerce.Orders.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EPiServer.Logging.Compatibility;
using EPiServer.Web;

using System.Net.Http;
using System;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Foundation.Features.Settings;
using Foundation.Infrastructure.Cms.Settings;
using Mediachase.Commerce.Customers;
using System.Collections.Specialized;
//using Foundation.Features.Checkout.Payments;
using Foundation.Features.Checkout.Payments;
using Foundation.Features.Checkout.ViewModels;
using Foundation.Features.Checkout.Services;
using Foundation.Infrastructure.Personalization;
using Foundation.Features.MyAccount.AddressBook;
using System.IO;
using System.Text;
using Mediachase.Commerce.Orders;
using System.Security.Cryptography;
using EPiServer.Globalization;
using EPiServer.ServiceLocation;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    /// <summary>
    /// Manage credit cards of user and organization
    /// </summary>
    [Authorize]
    public class CashfreeController : PageController<CashfreePage>
    {

        private readonly string _secretKey;

        private readonly IContentLoader _contentLoader;
        private readonly ICartService _cartService;
        private readonly IOrganizationService _organizationService;
        private readonly ICustomerService _customerService;
        private readonly IUrlResolver _urlResolver;
        private readonly ISettingsService _settingsService;
        //private readonly CustomerContext _customerContext;
        private readonly CheckoutService _checkoutService;
        private readonly ICashfreeService _cashfreeService;
        private readonly IOrderRepository _orderRepository;
        private readonly ICommerceTrackingService _recommendationService;
        private readonly ICashfreeClient _cashfreeClient;
        private readonly IAddressBookService _addressBookService;
        private CartWithValidationIssues _cart;


        public CashfreeController(
            IContentLoader contentLoader,
            ICartService cartService,
            IOrganizationService organizationService,
            ICustomerService customerService,
            IUrlResolver urlResolver,
            ISettingsService settingsService,
            //CustomerContext customerContext,
            CheckoutService checkoutService,
            ICashfreeService cashfreeService,
            IOrderRepository orderRepository,
            ICommerceTrackingService recommendationService,
            ICashfreeClient cashfreeClient,
            IAddressBookService addressBookService)
        {
            var configuration = new CashfreeConfiguration();
            _secretKey = configuration.SecretKey;

            _contentLoader = contentLoader;
            _cartService = cartService;
            _organizationService = organizationService;
            _customerService = customerService;
            _urlResolver = urlResolver;
            _settingsService = settingsService;
            //  _customerContext = customerContext;
            _checkoutService = checkoutService;
            _cashfreeService = cashfreeService;
            _orderRepository = orderRepository;
            _recommendationService = recommendationService;
            _cashfreeClient = cashfreeClient;
            _addressBookService = addressBookService;
        }

        [Obsolete]
        public async Task<IActionResult> HandleResponse(string order_id, string order_token)
        {
           // var cashfreeGateway = new CashfreePaymentGateway();
            ResponseModel data =await _cashfreeClient.GetOrderDetails(order_id,order_token);

            if (data.order_status == "PAID" || data.order_status == "paid")
            {
               var orderConfirmationUrl = await ProcessCashfreePayment();
                return Redirect(orderConfirmationUrl);

            }
            else if (data.order_status == "ACTIVE" || data.order_status == "active")
            {
                ResponseModel paymentDetails = await _cashfreeClient.GetCashfreePaymentDetails(order_id);
                if (paymentDetails.is_captured == true && paymentDetails.payment_status == "SUCCESS")
                {
                  var confirmationUrl =  await ProcessCashfreePayment();
                    return Redirect(confirmationUrl);
                }
                return null;

            }
            else if (data.order_status == "EXPIRED" || data.order_status == "expired")
            {
                return null;
            }
           
            // return null;
            return RedirectToAction("Index");
        }       

        [Obsolete]
        public async Task<string> ProcessCashfreePayment()
        {
            ModelState.Clear();
            //process the payment if paid
            if (PageEditing.PageIsInEditMode)

            {
                return null;
            }
            var configuration = new CashfreeConfiguration();
            var PaymentMethodId = configuration.PaymentMethodId;
            ICart currentCart = CartWithValidationIssues.Cart;
            if (!currentCart.Forms.Any() || !currentCart.GetFirstForm().Payments.Any())
            {
                throw new PaymentException(PaymentException.ErrorType.ProviderError, "", "Generic Error");
            }
            var purchaseOrder = _cashfreeService.PlaceOrder(currentCart, PaymentMethodId,ModelState);
            if (purchaseOrder == null)
            {
                TempData[Constant.ErrorMessages] = "There is no payment was processed";
                // return RedirectToAction("Index");
                return null;
            }
            CheckoutViewModel checkoutViewModel = new CheckoutViewModel();
            
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                var contact = _customerService.GetCurrentContact().Contact;
                var organization = contact.ContactOrganization;
                if (organization != null)
                {
                    purchaseOrder.Properties[Constant.Customer.CustomerFullName] = contact.FullName;
                    purchaseOrder.Properties[Constant.Customer.CustomerEmailAddress] = contact.Email;
                    purchaseOrder.Properties[Constant.Customer.CurrentCustomerOrganization] = organization.Name;
                    _orderRepository.Save(purchaseOrder);
                }
            }
            var confirmationSentSuccessfully = await _checkoutService.SendConfirmation(checkoutViewModel, purchaseOrder);

            await _recommendationService.TrackOrder(HttpContext, purchaseOrder);

            return _checkoutService.BuildRedirectionUrl(checkoutViewModel, purchaseOrder, confirmationSentSuccessfully);
           
        }
        private CartWithValidationIssues CartWithValidationIssues => _cart ?? (_cart = _cartService.LoadCart(_cartService.DefaultCartName, true));

        public async Task<IActionResult> NotifyUrl()
        {
            //3 steps to verify webhooks:-
            //Get the payload from the webhook endpoint.
            // Generate the signature.
            //Verify the signature

            //Get the payload from the webhook endpoint.
            await using var stream = new MemoryStream();
            await Request.Body.CopyToAsync(stream);
            var strRequest = Encoding.ASCII.GetString(stream.ToArray());

            // Generate the signature.
            var secretkey = _secretKey;

            var timestamp = Request.Headers["x-webhook-timestamp"];
            var signature = Request.Headers["x-webhook-signature"];
            var generatedSignature = ComputeSignature(secretkey, timestamp, strRequest);

            //verify the signature
            if (signature != generatedSignature)
            {
                return BadRequest("Failure in verifying signature");
            }


            dynamic result = JsonConvert.DeserializeObject(strRequest);
            var payment_status = result.data.payment.payment_status;
            var payment_message = result.data.payment.payment_message;
            var type = result.type;
            var order_id = result.data.order.order_id;
            var cf_payment_id = result.cf_payment_id;

            ////get the order by order_id
            //var order = await _orderService.GetOrderByIdAsync(Convert.ToInt32(order_id));

            //if (order == null)
            //    return Ok();

            var paymentStatus = PaymentStatus.Pending;
            if (payment_status == "FAILED" || payment_status == "failed" || payment_status == "USER_DROPPED" || payment_status == "user_dropped")
            {

                //USER_DROPPED==drop out of the payment flow without completing the transaction
                //cancel order
                // await _orderProcessingService.CancelOrderAsync(order, true);
                return null;

            }

            else if (payment_status == "SUCCESS" || payment_status == "success")
            {
                paymentStatus = PaymentStatus.Processed;
                var orderConfirmationUrl = await ProcessCashfreePayment();
                return Redirect(orderConfirmationUrl);
            }
            return Ok();
        }
        private string ComputeSignature(string secret, string timestamp, string strRequest)
        {
            var body = timestamp + strRequest;
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = encoding.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(body);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hashmessage);
            }

        }
    }
}