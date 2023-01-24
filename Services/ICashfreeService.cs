using EPiServer.Commerce.Order;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    /// <summary>
    /// All action on credit card data
    /// </summary>
    public interface ICashfreeService
    {
        //string GetDefaultCartName();
        //ICart LoadDefaultCart();
        //IPurchaseOrder ProcessSuccessfulTransaction(ICart cart, IPayment payment);
        //string ProcessUnsuccessfulTransaction(string cancelUrl, string errorMessage);
        IPurchaseOrder MakePurchaseOrder(ICart cart, IPayment payment);
        IPurchaseOrder PlaceOrder(ICart cart,Guid PaymentMethodId, ModelStateDictionary modelState);
    }
}
