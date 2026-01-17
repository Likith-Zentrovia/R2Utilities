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

namespace R2Utilities.Tasks.EmailTasks.WeeklyEmails;

public class PdaAddedToCartTask : EmailTaskBase
{
	private readonly EmailTaskService _emailTaskService;

	private readonly StringBuilder _failureEmailAddress = new StringBuilder();

	private readonly PdaEmailBuildService _pdaEmailBuildService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ResourceDiscountService _resourceDiscountService;

	private int _failCount;

	private int _successCount;

	public PdaAddedToCartTask(EmailTaskService emailTaskService, PdaEmailBuildService pdaEmailBuildService, IR2UtilitiesSettings r2UtilitiesSettings, ResourceDiscountService resourceDiscountService)
		: base("PdaAddedToCartTask", "-PdaAddedToCartTask", "46", TaskGroup.CustomerEmails, "Sends PDA resource add to cart emails to customers (IAs)", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_pdaEmailBuildService = pdaEmailBuildService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_resourceDiscountService = resourceDiscountService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "Pda Added To Cart Email Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "PdaAddedToCartTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			_successCount = 0;
			_failCount = 0;
			_pdaEmailBuildService.InitEmailTemplatesAddToCart();
			List<int> userIds = _emailTaskService.GetPdaAddedToCartUserIds();
			foreach (int userId in userIds)
			{
				List<PdaResource> pdaResources = _emailTaskService.GetPdaAddedToCartResources(userId);
				if (pdaResources.Any())
				{
					ProcessUser(userId, pdaResources);
				}
			}
			step.CompletedSuccessfully = true;
			string resultsBuilder = new StringBuilder().AppendFormat("<div>{0} PDA Added to Cart emails were sent</div>", _successCount).AppendLine().AppendFormat("<div>{0} emails failed</div>", _failCount)
				.AppendLine()
				.AppendFormat("<div>Emails that failed: {0}</div>", _failureEmailAddress)
				.AppendLine()
				.ToString();
			step.Results = resultsBuilder;
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

	public void ProcessUser(int userId, List<PdaResource> pdaResources)
	{
		User user = _emailTaskService.GetUser(userId);
		List<IResource> resources = _emailTaskService.GetResources(pdaResources.Select((PdaResource x) => x.ResourceId.GetValueOrDefault()).ToList());
		StringBuilder itemBuilder = new StringBuilder();
		AdminInstitution adminInstitution = new AdminInstitution(user.Institution);
		foreach (IResource resource in resources)
		{
			PdaResource pdaResource = pdaResources.FirstOrDefault((PdaResource x) => x.ResourceId == resource.Id);
			if (pdaResource != null)
			{
				pdaResource.ListPrice = resource.ListPrice;
				_resourceDiscountService.SetDiscount(pdaResource, adminInstitution);
				itemBuilder.Append(_pdaEmailBuildService.BuildPdaAddToCartItemTemplate(resource, user.Institution.Discount, pdaResource.AddedDate, pdaResource.AddedToCartDate, pdaResource.SpecialText, pdaResource.DiscountPrice, user.Institution.AccountNumber));
			}
		}
		List<User> territoryusers = new List<User>();
		if (user.Institution != null && user.Institution.Territory != null)
		{
			territoryusers = _emailTaskService.GetTerritoryOwners(user.Institution.Territory.Id);
		}
		string[] userArray = (territoryusers.Any() ? territoryusers.Select((User x) => x.Email).ToArray() : null);
		EmailMessage emailMessage = _pdaEmailBuildService.BuildPdaAddToCartEmail(itemBuilder.ToString(), user, userArray);
		if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
		{
			_successCount++;
			_emailTaskService.InsertPdaAddedToCartResourceEmail(userId);
		}
		else
		{
			_failCount++;
			_failureEmailAddress.AppendFormat("FailToSend: [InstitutionId:{0} | UserId:{1} | UserEmail: {2}] <br/>", (user.Institution != null) ? user.Institution.Id : 0, user.Id, user.Email);
		}
	}
}
