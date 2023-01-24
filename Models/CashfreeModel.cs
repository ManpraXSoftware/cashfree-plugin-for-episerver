using EPiServer.Framework.Localization;
using Foundation.Features.MyOrganization.Organization;
using Foundation.Infrastructure.Cms.Attributes;
using Mediachase.Commerce.Customers;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    /// <summary>
    /// Use to store detail information of Cashfree Create Order 
    /// </summary>
    public class CashfreeModel //: IDataErrorInfo
    {
        public string this[string columnName] => throw new NotImplementedException();

       // public string Error => throw new NotImplementedException();

        public string order_id { get; set; }
        public double order_amount { get; set; }
        public string order_currency { get; set; }
        public string order_note { get; set; }
        public CustomerDetails customer_details { get; set; }
        public OrderMeta order_meta { get; set; }
    }

    public class CustomerDetails
    {
        public string customer_id { get; set; }
        public string customer_name { get; set; }
        public string customer_email { get; set; }
        public string customer_phone { get; set; }
        public string customer_bank_account_number { get; set; }
        public string customer_bank_ifsc { get; set; }
        public string customer_bank_code { get; set; }


    }

    public partial class OrderMeta
    {
        public string return_url { get; set; }
        public string notify_url { get; set; }
        public string payment_methods { get; set; }

    }
}