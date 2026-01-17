using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Admin;
using R2V2.Core.Authentication;
using R2V2.Core.CollectionManagement;
using R2V2.Core.Email;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class ShoppingCartTask : EmailTaskBase
{
	private readonly EmailTaskService _emailTaskService;

	private readonly StringBuilder _failureEmailAddress = new StringBuilder();

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ResourceDiscountService _resourceDiscountService;

	private readonly ShoppingCartEmailBuildService _shoppingCartEmailBuildService;

	private int _failureToBuild;

	public ShoppingCartTask(EmailTaskService emailTaskService, ShoppingCartEmailBuildService shoppingCartEmailBuildService, IR2UtilitiesSettings r2UtilitiesSettings, ResourceDiscountService resourceDiscountService)
		: base("ShoppingCartTask", "-SendShoppingCartEmails", "43", TaskGroup.CustomerEmails, "Sends the shopping cart emails to customers (IAs)", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_shoppingCartEmailBuildService = shoppingCartEmailBuildService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_resourceDiscountService = resourceDiscountService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "Shopping Cart Email Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "ShoppingCartTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int userCount = 0;
		int successEmails = 0;
		int failureEmails = 0;
		try
		{
			List<ShoppingCartUser> shoppingCartUsers = _emailTaskService.GetShoppingCartUsers();
			foreach (ShoppingCartUser shoppingCartUser in shoppingCartUsers)
			{
				userCount++;
				R2UtilitiesBase.Log.InfoFormat("Processing {0} of {1} users - Id: {2}, username: {3}, email: {4}", userCount, shoppingCartUsers.Count(), shoppingCartUser.UserId, shoppingCartUser.UserName, shoppingCartUser.Email);
				Cart cart = _emailTaskService.GetCartForShoppingCartTask(shoppingCartUser.CartId);
				if (cart == null || !cart.CartItems.Any())
				{
					continue;
				}
				if (cart.CartItems.Count((CartItem result) => result.ResourceId.HasValue) < 1)
				{
					R2UtilitiesBase.Log.InfoFormat(" 1/3/1013 Fix - Caught a cart that does not have any resources in it.");
					continue;
				}
				R2UtilitiesBase.Log.InfoFormat("Items in Cart for Institution: {0}", shoppingCartUser.InstitutionId);
				AdminInstitution adminInstitution = _emailTaskService.GetAdminInstitution(shoppingCartUser.InstitutionId);
				foreach (CartItem item in cart.CartItems)
				{
					R2UtilitiesBase.Log.InfoFormat("ResourceId: {0} || ProductId: {1}", item.ResourceId, item.ProductId);
					R2UtilitiesBase.Log.InfoFormat("Resource Discount Price: {0}", item.DiscountPrice);
					_resourceDiscountService.SetDiscount(item, adminInstitution);
				}
				EmailMessage emailMessage = ProcessCartEmail(shoppingCartUser, cart, adminInstitution);
				if (emailMessage != null)
				{
					if (EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
					{
						successEmails++;
					}
					else
					{
						failureEmails++;
					}
				}
			}
			step.CompletedSuccessfully = failureEmails == 0 && _failureToBuild == 0;
			step.Results = $"{successEmails} shopping cart report emails sent, {_failureToBuild} emails failed to build, {failureEmails} emails failed to send. Failed Emails information: {_failureEmailAddress}";
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			step.CompletedSuccessfully = false;
			step.Results = ex.Message;
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	public EmailMessage ProcessCartEmail(ShoppingCartUser shoppingCartUser, Cart cart, AdminInstitution adminInstitution)
	{
		_shoppingCartEmailBuildService.ClearParameters();
		EmailMessage emailMessage = null;
		bool displayInstitutionDiscount = true;
		try
		{
			foreach (CartItem cartItem in cart.CartItems)
			{
				IResource resource = null;
				if (cartItem.ResourceId.HasValue)
				{
					resource = _emailTaskService.GetResource(cartItem.ResourceId.Value);
					if (resource == null)
					{
						continue;
					}
				}
				displayInstitutionDiscount = displayInstitutionDiscount && cartItem.Discount == adminInstitution.Discount;
				_shoppingCartEmailBuildService.BuildItemHtml(cartItem, resource, _r2UtilitiesSettings.SpecialIconBaseUrl, shoppingCartUser.AccountNumber);
			}
			if (_shoppingCartEmailBuildService.ShouldEmailBeProcessed())
			{
				User user = _emailTaskService.GetUser(shoppingCartUser.UserId);
				emailMessage = _shoppingCartEmailBuildService.BuildShoppingCartEmail(user, displayInstitutionDiscount);
			}
			_shoppingCartEmailBuildService.ClearParameters();
			return emailMessage;
		}
		catch (Exception ex)
		{
			_failureToBuild++;
			_failureEmailAddress.AppendFormat("FailToBuild: [InstitutionId:{0} | UserId:{1} | UserEmail: {2}] <br/>", shoppingCartUser.InstitutionId, shoppingCartUser.UserId, shoppingCartUser.Email);
			R2UtilitiesBase.Log.InfoFormat("Failed to Build Cart | cartId : {0} || error: {1}", cart.Id, ex);
		}
		return emailMessage;
	}
}
