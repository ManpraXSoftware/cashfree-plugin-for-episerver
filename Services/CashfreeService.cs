using EPiServer.Commerce.Order;
using EPiServer.Data;
using EPiServer.Framework.Localization;
using EPiServer.Logging.Compatibility;
using EPiServer.ServiceLocation;
using Foundation.Features.Checkout.Payments.Cashfree;
using Foundation.Features.MyOrganization.Organization;
using Foundation.Infrastructure.Commerce.Customer;
using Foundation.Infrastructure.Commerce.Customer.Services;
using Mediachase.BusinessFoundation.Data;
using Mediachase.BusinessFoundation.Data.Business;
using Mediachase.Commerce.Customers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EPiServer.Security;
using EPiServer.Web;
using Mediachase.Commerce.Orders;
using Mediachase.Commerce.Orders.Managers;
using Mediachase.Commerce.Security;
using Microsoft.AspNetCore.Http;
using System.Web;
using Foundation.Features.Checkout.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Mediachase.Commerce.Orders.Exceptions;
using Foundation.Features.Checkout.Services;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    /// <summary>
    /// All action on cashfree
    /// </summary>
    public class CashfreeService : ICashfreeService
    {

        private readonly IHttpContextAccessor _httpContextAccessor;

        [Obsolete]
        private readonly ILog _logger = LogManager.GetLogger(typeof(CashfreeService));
            
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderGroupCalculator _orderGroupCalculator;
        private readonly ILoyaltyService _loyaltyService;
        private readonly LocalizationService _localizationService;
        private static readonly Lazy<DatabaseMode> _databaseMode = new Lazy<DatabaseMode>(() => GetDefaultDatabaseMode());

        public CashfreeService() : this(ServiceLocator.Current.GetInstance<IOrderRepository>(),
            ServiceLocator.Current.GetInstance<IOrderGroupCalculator>(),
            ServiceLocator.Current.GetInstance<IHttpContextAccessor>(),
            ServiceLocator.Current.GetInstance<ILoyaltyService>(),
            ServiceLocator.Current.GetInstance<LocalizationService>())
        { }

        public CashfreeService(IOrderRepository orderRepository,
            IOrderGroupCalculator orderGroupCalculator, 
            IHttpContextAccessor httpContextAccessor,
            ILoyaltyService loyaltyService,
            LocalizationService localizationSrvice)
        {
            _orderRepository = orderRepository;
            _orderGroupCalculator = orderGroupCalculator;
            _httpContextAccessor = httpContextAccessor;
            _loyaltyService = loyaltyService;
            _localizationService = localizationSrvice;
        }


        public virtual IPurchaseOrder PlaceOrder(ICart cart, Guid PaymentMethodId, ModelStateDictionary modelState)
        {
            try
            {
                var payment = cart.Forms.SelectMany(f => f.Payments).FirstOrDefault(c => c.PaymentMethodId.Equals(PaymentMethodId));
                if (payment == null)
                {
                    throw new PaymentException(PaymentException.ErrorType.ProviderError, "", "Payment Not Specified");
                }
                payment.Status = PaymentStatus.Processed.ToString();
                var processedPayments = cart.GetFirstForm().Payments.Where(x => x.Status.Equals(PaymentStatus.Processed.ToString()));

                var totalProcessedAmount = processedPayments.Sum(x => x.Amount);
                if (totalProcessedAmount != cart.GetTotal(_orderGroupCalculator).Amount)
                {
                    throw new InvalidOperationException("Wrong amount");
                }

                // var orderReference = cart.Properties["IsUsePaymentPlan"] != null && cart.Properties["IsUsePaymentPlan"].Equals(true) ? _checkoutService.SaveAsPaymentPlan(cart) : _orderRepository.SaveAsPurchaseOrder(cart);
                var orderReference = _orderRepository.SaveAsPurchaseOrder(cart);
                var purchaseOrder = _orderRepository.Load<IPurchaseOrder>(orderReference.OrderGroupId);
                AddNoteToPurchaseOrder(string.Empty, $"New order placed by {PrincipalInfo.CurrentPrincipal.Identity.Name} in Public site", Guid.Empty, purchaseOrder);                
                _orderRepository.Delete(cart.OrderLink);
                purchaseOrder.OrderStatus = OrderStatus.InProgress;
                cart.AdjustInventoryOrRemoveLineItems((item, validationIssue) => { });
                //Loyalty Program: Add Points and Number of orders
                _loyaltyService.AddNumberOfOrders();
                _orderRepository.Save(purchaseOrder);

                return purchaseOrder;
            }
            catch (PaymentException ex)
            {
                modelState.AddModelError("", _localizationService.GetString("/Checkout/Payment/Errors/ProcessingPaymentFailure") + ex.Message);
            }
            catch (Exception ex)
            {
                modelState.AddModelError("", ex.Message);
            }

            return null;
        }

        public string GetDefaultCartName() => Cart.DefaultName;

        public ICart LoadDefaultCart() => _orderRepository.LoadCart<ICart>(PrincipalInfo.CurrentPrincipal.GetContactId(), GetDefaultCartName());
        public IPurchaseOrder MakePurchaseOrder(ICart cart, IPayment payment)
        {
            var orderReference = _orderRepository.SaveAsPurchaseOrder(cart);
            var purchaseOrder = _orderRepository.Load<IPurchaseOrder>(orderReference.OrderGroupId);
          //  purchaseOrder.OrderNumber = payment.Properties[Constant.OrderCode] as string;

            if (_databaseMode.Value != DatabaseMode.ReadOnly)
            {
                // Update last order date time for CurrentContact
                UpdateLastOrderTimestampOfCurrentContact(CustomerContext.Current.CurrentContact, purchaseOrder.Created);
            }

            AddNoteToPurchaseOrder(string.Empty, $"New order placed by {PrincipalInfo.CurrentPrincipal.Identity.Name} in Public site", Guid.Empty, purchaseOrder);

            // Remove old cart
            _orderRepository.Delete(cart.OrderLink);
            purchaseOrder.OrderStatus = OrderStatus.InProgress;

            _orderRepository.Save(purchaseOrder);

            return purchaseOrder;
        }

        [Obsolete]
        public IPurchaseOrder ProcessSuccessfulTransaction(ICart cart, IPayment payment)
        {
            if (_httpContextAccessor == null || cart == null)
            {
                return null;
            }
            var paymentSuccess = ProcessPayment(cart);

            if (!paymentSuccess)
            {
                _logger.Error("Can not process payment");
                return null;
            }

            // Place order
            return MakePurchaseOrder(cart, payment);
        }
        public string ProcessUnsuccessfulTransaction(string cancelUrl, string errorMessage)
        {
            if (_httpContextAccessor == null)
            {
                return cancelUrl;
            }

            _logger.Error($"Cashfree transaction failed [{errorMessage}].");
            return UriUtil.AddQueryString(cancelUrl, "message", HttpUtility.UrlEncode(errorMessage));
        }
        public virtual bool ProcessPayment(ICart cart)
        {
            try
            {
                foreach (IPayment p in cart.Forms.SelectMany(f => f.Payments).Where(p => p != null))
                {
                    PaymentStatusManager.ProcessPayment(p);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Process Payment Failed", ex);
                return false;
            }
        }
        protected void AddNoteToPurchaseOrder(string title, string detail, Guid customerId, IPurchaseOrder purchaseOrder)
        {
            var orderNote = purchaseOrder.CreateOrderNote();
            orderNote.Type = OrderNoteTypes.System.ToString();
            orderNote.CustomerId = customerId != Guid.Empty ? customerId : PrincipalInfo.CurrentPrincipal.GetContactId();
            orderNote.Title = !string.IsNullOrEmpty(title) ? title : detail.Substring(0, Math.Min(detail.Length, 24)) + "...";
            orderNote.Detail = detail;
            orderNote.Created = DateTime.UtcNow;
            purchaseOrder.Notes.Add(orderNote);
        }
        private static DatabaseMode GetDefaultDatabaseMode()
        {
            if (!_databaseMode.IsValueCreated)
            {
                return ServiceLocator.Current.GetInstance<IDatabaseMode>().DatabaseMode;
            }
            return _databaseMode.Value;
        }
        /// <summary>
        /// Update last order time stamp which current user completed.
        /// </summary>
        /// <param name="contact">The customer contact.</param>
        /// <param name="datetime">The order time.</param>
        protected void UpdateLastOrderTimestampOfCurrentContact(CustomerContact contact, DateTime datetime)
        {
            if (contact != null)
            {
                contact.LastOrder = datetime;
                contact.SaveChanges();
            }
        }

    }
}