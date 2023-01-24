using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    /// <summary>
    /// Use to store detail information of Cashfree Response after create order
    /// </summary>
    public class ResponseModel
    {
        public string order_status { get;set; }
        public string payment_status { get; set; }
        public bool is_captured { get; set; }
        public string payment_link { get; set; }
        public string order_token { get; set; }
        public string url { get; set; }
        public Data data { get; set; }
        public string cf_payment_id { get; set; }
        /// <summary>
        /// Subscription Response Data
        /// </summary>
        public string planId { get; set; }
        public string authLink { get; set; }
        public string subStatus { get; set; }
        public string subReferenceId { get; set; }
        public string message { get; set; }
        public string status { get; set; } 
        public Customer_Details customer_details { get; set; }
        public string customer_id { get; set; }
    }

    public class Data
    {
        public string url { get; set; }
    }
    public class Customer_Details 
    {
        public string customer_id { get; set; }
    }
}
