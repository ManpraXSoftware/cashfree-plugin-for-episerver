using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using Foundation.Features.Shared;
using Foundation.Features.Shared.EditorDescriptors;
using Foundation.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace Foundation.Features.Checkout.Payments.Cashfree
{
    [ContentType(DisplayName = "Cashfree Page",
        GUID = "9250F9EB-4683-4A85-8793-60714F7D6B58",
        Description = "Manage cashfree",
        AvailableInEditMode = false,
        GroupName = "Payment",
        Order = 100)]
    [ImageUrl("/icons/cms/pages/CMS-icon-page-30.png")]
    public class CashfreePage : FoundationPageData
    {
        
    }
}