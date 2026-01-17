using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.CollectionManagement;
using R2V2.Core.Email;
using R2V2.Core.Institution;
using R2V2.Core.OrderHistory;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.UnitOfWork;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class OrderSummaryTask : EmailTaskBase, ITask
{
	private readonly IQueryable<Cart> _carts;

	private readonly IQueryable<Institution> _institutions;

	private readonly IQueryable<DbOrderHistory> _orderHistories;

	private readonly OrderSummaryEmailBuildService _orderSummaryEmailBuildService;

	private readonly IQueryable<Product> _products;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly IQueryable<IResource> _resources;

	private readonly IUnitOfWork _unitOfWork;

	private DateTime _OrderSummaryDate;

	public OrderSummaryTask(IR2UtilitiesSettings r2UtilitiesSettings, IUnitOfWork unitOfWork, IQueryable<Cart> carts, IQueryable<DbOrderHistory> orderHistories, IQueryable<Institution> institutions, IQueryable<IResource> resources, IQueryable<Product> products, OrderSummaryEmailBuildService orderSummaryEmailBuildService)
		: base("OrderSummaryTask", "-OrderSummaryTask", "60", TaskGroup.InternalSystemEmails, "Sends the system wide order summary email", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_unitOfWork = unitOfWork;
		_carts = carts;
		_orderHistories = orderHistories;
		_institutions = institutions;
		_resources = resources;
		_products = products;
		_orderSummaryEmailBuildService = orderSummaryEmailBuildService;
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_OrderSummaryDate = GetArgumentDateTime("orderSummaryDate", DateTime.Now);
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		R2UtilitiesBase.Log.Info($"-job: OrderSummaryTask, -orderSummaryDate: {_OrderSummaryDate}");
	}

	public override void Run()
	{
		base.TaskResult.Information = "Order Summary Task Run";
		TaskResultStep step = new TaskResultStep
		{
			Name = "OrderSummaryTask Run",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			List<DbOrderHistory> orderHistories = _orderHistories.Where((DbOrderHistory x) => x.PurchaseDate.Date == ((DateTime)_OrderSummaryDate).Date).ToList();
			bool succeed = ProcessOrderSummary(orderHistories);
			step.CompletedSuccessfully = succeed;
			step.Results = "Order Summary Task completed successfully";
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

	public bool ProcessOrderSummary(List<DbOrderHistory> orderHistories)
	{
		base.TaskResult.Information = "Process Order Summary";
		TaskResultStep step = new TaskResultStep
		{
			Name = "ProcessOrderSummary (Build Email)",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		bool success = false;
		try
		{
			int totalOrders = 0;
			int totalTitles = 0;
			int totalLicenses = 0;
			int totalMaintenanceFee = 0;
			decimal totalSales = default(decimal);
			foreach (DbOrderHistory orderHistory in orderHistories)
			{
				decimal orderHistoryTotal = default(decimal);
				Institution institution = _institutions.FirstOrDefault((Institution x) => x.Id == orderHistory.InstitutionId);
				string institutionInfo = "";
				if (institution != null)
				{
					institutionInfo = institution.Name + "<br/>" + institution.AccountNumber;
				}
				StringBuilder resourceItems = new StringBuilder();
				StringBuilder productItems = new StringBuilder();
				List<DbOrderHistoryItem> orderHistoryItems = orderHistory.OrderHistoryItems.ToList();
				IEnumerable<int> resourceIds = from x in orderHistoryItems
					where x.ResourceId.HasValue
					select x.ResourceId.Value;
				IEnumerable<int> productIds = from x in orderHistoryItems
					where x.ProductId.HasValue
					select x.ProductId.Value;
				List<IResource> orderHistoryResources = _resources.Where((IResource x) => resourceIds.Contains(x.Id)).ToList();
				List<Product> orderHistoryProducts = _products.Where((Product x) => productIds.Contains(x.Id)).ToList();
				foreach (DbOrderHistoryItem orderHistoryItem in orderHistoryItems)
				{
					if (orderHistoryItem.ProductId.HasValue && orderHistoryItem.ProductId > 0)
					{
						Product product = orderHistoryProducts.FirstOrDefault((Product x) => x.Id == orderHistoryItem.ProductId.Value) ?? new Product
						{
							Name = "N/A"
						};
						productItems.Append("<br/>");
						productItems.AppendFormat("{0}", product.Name);
						orderHistoryTotal += orderHistoryItem.DiscountPrice;
						int id = product.Id;
						int num = id;
						if (num == 1)
						{
							totalMaintenanceFee++;
						}
						continue;
					}
					IResource resource = orderHistoryResources.FirstOrDefault((IResource x) => x.Id == orderHistoryItem.ResourceId);
					if (resource != null)
					{
						if (resourceItems.Length > 0)
						{
							resourceItems.Append("<br/>");
						}
						totalTitles++;
						resourceItems.AppendFormat("{0} ({1})", resource.Isbn, orderHistoryItem.NumberOfLicenses);
						if (orderHistoryItem.IsBundle)
						{
							orderHistoryTotal += orderHistoryItem.DiscountPrice;
						}
						else if (!resource.IsFreeResource)
						{
							orderHistoryTotal += orderHistoryItem.DiscountPrice * (decimal)orderHistoryItem.NumberOfLicenses;
						}
						totalLicenses += orderHistoryItem.NumberOfLicenses;
					}
					else
					{
						R2UtilitiesBase.Log.DebugFormat($"Error with OrderHistoryItem: {orderHistoryItem.Id} in OrderHistory: {orderHistory.Id}");
					}
				}
				totalSales += orderHistoryTotal;
				totalOrders++;
				_orderSummaryEmailBuildService.BuildItemHtml(orderHistory, institutionInfo, resourceItems.ToString(), productItems.ToString(), orderHistoryTotal);
			}
			EmailMessage emailMessage = _orderSummaryEmailBuildService.BuildOrderSummaryEmail(totalOrders, totalSales, totalTitles, totalLicenses, totalMaintenanceFee, base.EmailSettings.TaskEmailConfig.ToAddresses.ToArray());
			if (emailMessage != null)
			{
				AddTaskCcToEmailMessage(emailMessage);
				AddTaskBccToEmailMessage(emailMessage);
				success = EmailDeliveryService.SendTaskReportEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName);
			}
			step.CompletedSuccessfully = true;
			step.Results = "Process Order Summary completed successfully";
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
		return success;
	}
}
