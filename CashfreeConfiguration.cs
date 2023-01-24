using Mediachase.Commerce.Core;
using Mediachase.Commerce.Orders.Dto;
using Mediachase.Commerce.Orders.Managers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    /// <summary>
    /// Use to store and save detail information of Cashfree Configuration 
    /// Use to save parameters (Settings) added in the Cashfree Configuration
    /// </summary>
    public class CashfreeConfiguration
    {
        private PaymentMethodDto _paymentMethodDto;
        private IDictionary<string, string> _settings;

        public const string SystemName = "CashfreePayment"; //Cashfree payment method name/system keyword

       public const string CashfreeApiUrlParameter = "CashfreeApiUrl";
        public const string AppIDParameter = "AppID";
        public const string SecretKeyParameter = "SecretKey";
        public const string ApiVersionParameter = "ApiVersion";
        public const string ActiveEnvironmentParameter = "ActiveEnviroment";


        public string CashfreeApiUrl { get;  set; }
        public string AppID { get;  set; }
        public string SecretKey { get;  set; }
        public string ApiVersion { get;  set; }
        public string ActiveEnvironment { get;  set; }

        public Guid PaymentMethodId { get; protected set; }

        /// <summary>
        /// Initializes a new instance of <see cref="CashfreeConfigurationn"/>.
        /// </summary>
        public CashfreeConfiguration() :
            this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="CashfreeConfigurationn"/> with specific settings.
        /// </summary>
        /// <param name="settings">The specific settings.</param>
        public CashfreeConfiguration(IDictionary<string, string> settings)
        {
            Initialize(settings);
        }

        public static PaymentMethodDto.PaymentMethodParameterRow GetParameterByName(PaymentMethodDto paymentMethodDto, string parameterName)
        {
            var rowArray = (PaymentMethodDto.PaymentMethodParameterRow[])paymentMethodDto.PaymentMethodParameter.Select($"Parameter = '{parameterName}'");
            return rowArray.Length > 0 ? rowArray[0] : null;
        }

        protected virtual void Initialize(IDictionary<string, string> settings)
        {
            _paymentMethodDto = GetCashfreePaymentMethod();
            PaymentMethodId = GetPaymentMethodId();

            _settings = settings ?? GetSettings();
            GetParametersValues();
        }

        public static PaymentMethodDto GetCashfreePaymentMethod()
        {
            return PaymentManager.GetPaymentMethodBySystemName(SystemName, SiteContext.Current.LanguageName);
        }

        private Guid GetPaymentMethodId()
        {
            var paymentMethodRow = _paymentMethodDto.PaymentMethod.Rows[0] as PaymentMethodDto.PaymentMethodRow;
            return paymentMethodRow?.PaymentMethodId ?? Guid.Empty;
        }

        private IDictionary<string, string> GetSettings()
        {
            return _paymentMethodDto.PaymentMethod
                .FirstOrDefault()
                ?.GetPaymentMethodParameterRows()
                ?.ToDictionary(row => row.Parameter, row => row.Value);
        }
        private void GetParametersValues()
        {
            if (_settings != null)
            {
               CashfreeApiUrl = GetParameterValue(CashfreeApiUrlParameter);
                AppID = GetParameterValue(AppIDParameter);
                SecretKey = GetParameterValue(SecretKeyParameter);
                ApiVersion = GetParameterValue(ApiVersionParameter);
                ActiveEnvironment = GetParameterValue(ActiveEnvironmentParameter);
            }
        }

        private string GetParameterValue(string parameterName)
        {
            string parameterValue;
            return _settings.TryGetValue(parameterName, out parameterValue) ? parameterValue : string.Empty;
        }
    }
}
