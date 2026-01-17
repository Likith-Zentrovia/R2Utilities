using System;
using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.CollectionManagement;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class CartMaintenanceTask : TaskBase, ITask
{
	private readonly CartFactory _cartFactory;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public CartMaintenanceTask(CartFactory cartFactory, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("CartMaintenanceTask", "-CartMaintenanceTask", "83", TaskGroup.ContentLoading, "Deletes Auto Carts that have not been converted to Saved or Purchased after X days", enabled: true)
	{
		_cartFactory = cartFactory;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will Delete Auto Carts older than AutoCartDeleteOlderInDays.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "AutomatedCartTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			StringBuilder stepResults = new StringBuilder();
			stepResults.Append("Carts Deleted: InstitutionId/CartId");
			List<Cart> carts = _cartFactory.GetAutoCartsToDelete(_r2UtilitiesSettings.AutoCartDeleteOlderInDays);
			foreach (Cart cart in carts)
			{
				stepResults.Append($"{cart.InstitutionId}/{cart.Id}");
			}
			if (_cartFactory.DeleteCarts(carts))
			{
				step.Results = stepResults.ToString();
				step.CompletedSuccessfully = true;
			}
			else
			{
				step.Results = "Error Deleting Carts";
				step.CompletedSuccessfully = false;
			}
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
}
