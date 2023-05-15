using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Marketplace.SaaS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CAS.ListEnrichment.Controllers
{
    [Authorize(AuthenticationSchemes = OpenIdConnectDefaults.AuthenticationScheme)]
    [AuthorizeForScopes(Scopes = new string[] { "user.read" })]
    public class HomeController : Controller
    {
        private readonly IMarketplaceSaaSClient _marketplaceSaaSClient;
        private readonly GraphServiceClient _graphServiceClient;

        public HomeController(
            IMarketplaceSaaSClient marketplaceSaaSClient,
            GraphServiceClient graphServiceClient)
        {
            _marketplaceSaaSClient = marketplaceSaaSClient;
            _graphServiceClient = graphServiceClient;
        }

        /// <summary>
        /// Shows all information associated with the user, the request, and the subscription.
        /// </summary>
        /// <param name="token">THe marketplace purchase ID token</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public async Task<IActionResult> IndexAsync(string token, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    this.ModelState.AddModelError(string.Empty, "Token URL parameter cannot be empty");
                    this.ViewBag.Message = "Token URL parameter cannot be empty";
                    return this.View();
                }

                // resolve the subscription using the marketplace purchase id token
                var resolvedSubscription = (await _marketplaceSaaSClient.Fulfillment.ResolveAsync("Bearer " + token, cancellationToken: cancellationToken)).Value;

                // get the plans on this subscription
                var subscriptionPlans = (await _marketplaceSaaSClient.Fulfillment.ListAvailablePlansAsync(resolvedSubscription.Id.Value, cancellationToken: cancellationToken)).Value;

                // find the plan that goes with this purchase
                string planName = string.Empty;
                foreach (var plan in subscriptionPlans.Plans)
                {
                    if (plan.PlanId == resolvedSubscription.Subscription.PlanId)
                    {
                        planName = plan.DisplayName;
                    }
                }

                // get graph current user data
                var graphApiUser = await _graphServiceClient.Me.Request().GetAsync();

                // build the model
                var model = new IndexViewModel
                {
                    DisplayName = graphApiUser.DisplayName,
                    Email = graphApiUser.Mail,
                    SubscriptionName = resolvedSubscription.SubscriptionName,
                    FulfillmentStatus = resolvedSubscription.Subscription.SaasSubscriptionStatus.GetValueOrDefault(),
                    PlanName = planName,
                    SubscriptionId = resolvedSubscription.Id.ToString(),
                    TenantId = resolvedSubscription.Subscription.Beneficiary.TenantId.ToString(),
                    PurchaseIdToken = token,
                    AllDetails = new DetailsViewModel() //// Fill in the details
                    {
                        PurchaseIdToken = token,
                        UserClaims = this.User.Claims,
                        GraphUser = graphApiUser,
                        Subscription = resolvedSubscription.Subscription,
                        SubscriptionPlans = subscriptionPlans
                    }
                };

                TempData["CurrentSubscription"] = model;

                return View(model);

            }
            catch (Exception exception)
            {
                return View(new IndexViewModel()
                {
                    AllDetails = new DetailsViewModel()
                    {
                        UserClaims = new List<Claim>(),
                        SubscriptionPlans =null,
                        GraphUser = new User(),
                        OperationList = null,
                        PurchaseIdToken = String.Empty,
                        Subscription =null
                    }
                }); ; //// Temporary for now
            }
        }

        public async Task<IActionResult> SaveDetails(CancellationToken cancellationToken)
        {

            IndexViewModel indexModel = (IndexViewModel)TempData["CurrentSubscription"];

            if(indexModel!= null)
            {
                var jsonString = JsonConvert.SerializeObject(indexModel);

                ////  This will be call to Activate subscription.
                ///   Second parameter is yet to be derived here. 
                ///   Or just save user data
                ///  var operationId = await _marketplaceSaaSClient.Fulfillment.ActivateSubscriptionAsync(indexModel.SubscriptionId,, cancellationToken: cancellationToken);

                this.WriteToFile(jsonString);
            }
           

            return View();
        }

        private void WriteToFile(string fileText)
        {
            // Creating a file
            string myfile = @"SubscribersData.txt";

            // Checking the above file
            if (!System.IO.File.Exists(myfile))
            {
                // Creating the same file if it doesn't exist
                using (StreamWriter sw = System.IO.File.CreateText(myfile))
                {
                    sw.WriteLine(fileText);
                }
            }

            // Appending the given texts
            using (StreamWriter sw = System.IO.File.AppendText(myfile))
            {
                sw.WriteLine(fileText);
            }
        }
    }
}
