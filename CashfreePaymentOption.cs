using EPiServer.Commerce.Order;
using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;
using Foundation.Infrastructure.Commerce.Markets;
using Mediachase.Commerce;
using Mediachase.Commerce.Orders;
using System.ComponentModel;
using Foundation.Features.Checkout.Payments.Cashfree;
using Mediachase.Commerce.Orders.Dto;
using System.Linq;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    [ServiceConfiguration(typeof(IPaymentMethod))]
    public class CashfreePaymentOption : PaymentOptionBase //, IDataErrorInfo
    {
        public string AppID { get; set; } 
        public string ApiVersion { get; set; }
        public string SecretKey { get; set; }
        public string ActiveEnvironment { get; set; }
        public override string SystemKeyword { get; } = "CashfreePayment";  //this will be same as systemkeyword in the payment


        public CashfreePaymentOption()
           : this(LocalizationService.Current,
                 ServiceLocator.Current.GetInstance<IOrderGroupFactory>(), 
                 ServiceLocator.Current.GetInstance<ICurrentMarket>(),
                 ServiceLocator.Current.GetInstance<LanguageService>(),
                 ServiceLocator.Current.GetInstance<IPaymentService>())
        {
        }

       private readonly IOrderGroupFactory _orderGroupFactory;

        private readonly PaymentMethodDto.PaymentMethodRow _paymentMethod;



        public CashfreePaymentOption(LocalizationService localizationService,
            IOrderGroupFactory orderGroupFactory,
            ICurrentMarket currentMarket,
            LanguageService languageService,
            IPaymentService paymentService)
            : base(localizationService, orderGroupFactory, currentMarket, languageService, paymentService)
        {
            _orderGroupFactory = orderGroupFactory;

            var paymentMethodDto = CashfreeConfiguration.GetCashfreePaymentMethod();
            _paymentMethod = paymentMethodDto?.PaymentMethod?.FirstOrDefault();

        }

        public override IPayment CreatePayment(decimal amount, IOrderGroup orderGroup)
        {
            var payment = orderGroup.CreatePayment(OrderGroupFactory);
            payment.PaymentMethodId = PaymentMethodId;
            payment.PaymentMethodName = SystemKeyword;
            payment.Amount = amount;
            payment.PaymentType = PaymentType.Other; 
            payment.Status = PaymentStatus.Pending.ToString();
            payment.TransactionType = TransactionType.Authorization.ToString();

            return payment;

        }

        public override bool ValidateData() => true;
    }
}