using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using R2Library.Data.ADO.R2Utility;
using R2V2.Core.Authentication;
using R2V2.Core.Institution;
using R2V2.Infrastructure.UnitOfWork;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class MinimizeIpRangesTask : TaskBase
{
	private readonly IQueryable<IpAddressRange> _ipAddressRanges;

	private readonly IUnitOfWorkProvider _unitOfWorkProvider;

	public MinimizeIpRangesTask(IQueryable<IpAddressRange> ipAddressRanges, IUnitOfWorkProvider unitOfWorkProvider)
		: base("MinimizeIpRangesTask", "-MinimizeIpRangesTask", "13", TaskGroup.ContentLoading, "Task to minimize or group institution IP ranges", enabled: true)
	{
		_ipAddressRanges = ipAddressRanges;
		_unitOfWorkProvider = unitOfWorkProvider;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will condense the number of IP ranges for institutions if the ranges are consecetive.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "MinimizeIpRangesTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int ipRangesMerged = 0;
		int ipRangesDeleted = 0;
		try
		{
			StringBuilder sb = new StringBuilder();
			StringBuilder results = new StringBuilder();
			List<Institution> institutions = _ipAddressRanges.Select((IpAddressRange x) => x.Institution).Distinct().ToList();
			foreach (Institution institution in institutions)
			{
				Dictionary<IpAddressRange, bool> ipRangesChanged = ProcessIpRanges(institution.Id);
				if (ipRangesChanged.Any() && ipRangesChanged.Count > 0)
				{
					results.AppendFormat("<div>Institution: {0} has had {1} Ip Ranges changed</div>", institution.Id, ipRangesChanged.Count);
				}
				if (!ipRangesChanged.Any())
				{
					continue;
				}
				foreach (KeyValuePair<IpAddressRange, bool> pair in ipRangesChanged)
				{
					if (pair.Value)
					{
						ipRangesMerged++;
					}
					else
					{
						ipRangesDeleted++;
					}
					sb.AppendFormat("Institution {2} has had the following {0} : {1}", pair.Value ? "Updated" : "Deleted", pair.Key.ToAuditString(), pair.Key.InstitutionId).AppendLine();
				}
			}
			R2UtilitiesBase.Log.Info(sb.ToString());
			R2UtilitiesBase.Log.InfoFormat("{0} IPs updated and {1} deleted", ipRangesMerged, ipRangesDeleted);
			step.Results = results.ToString();
			step.CompletedSuccessfully = true;
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

	private Dictionary<IpAddressRange, bool> ProcessIpRanges(int institutionId)
	{
		IQueryable<IpAddressRange> ipRanges = from x in _ipAddressRanges
			where x.InstitutionId == institutionId
			orderby x.IpNumberStart
			select x;
		Dictionary<IpAddressRange, bool> ipAddressRanges = new Dictionary<IpAddressRange, bool>();
		IpAddressRange lastIpAddressRange = null;
		bool lastWasChanged = false;
		bool saveLast = false;
		foreach (IpAddressRange ipAddressRange in ipRanges)
		{
			if (lastIpAddressRange == null)
			{
				lastIpAddressRange = ipAddressRange;
			}
			else if (ipAddressRange.IpNumberStart == lastIpAddressRange.IpNumberEnd + 1)
			{
				lastIpAddressRange.OctetCEnd = ipAddressRange.OctetCEnd;
				lastIpAddressRange.OctetDEnd = ipAddressRange.OctetDEnd;
				lastIpAddressRange.IpNumberEnd = ipAddressRange.IpNumberEnd;
				ipAddressRanges.Add(ipAddressRange, value: false);
				lastWasChanged = true;
				saveLast = true;
			}
			else
			{
				if (lastWasChanged)
				{
					ipAddressRanges.Add(lastIpAddressRange, value: true);
				}
				lastWasChanged = false;
				lastIpAddressRange = ipAddressRange;
				saveLast = false;
			}
		}
		if (saveLast)
		{
			ipAddressRanges.Add(lastIpAddressRange, value: true);
		}
		SaveDeleteIpRanges(ipAddressRanges.OrderByDescending((KeyValuePair<IpAddressRange, bool> x) => x.Value));
		return ipAddressRanges;
	}

	private void SaveDeleteIpRanges(IEnumerable<KeyValuePair<IpAddressRange, bool>> ipRangesToSaveOrDelete)
	{
		foreach (KeyValuePair<IpAddressRange, bool> item in ipRangesToSaveOrDelete)
		{
			KeyValuePair<IpAddressRange, bool> pair = item;
			using IUnitOfWork uow = _unitOfWorkProvider.Start();
			try
			{
				IpAddressRange dbIpAddressRange = _ipAddressRanges.FirstOrDefault((IpAddressRange x) => x.Id == ((KeyValuePair<IpAddressRange, bool>)pair).Key.Id);
				if (dbIpAddressRange != null)
				{
					if (pair.Value)
					{
						string sql = new StringBuilder().Append("UPDATE tIpAddressRange ").Append("   SET tiOctetCEnd = :tiOctetCEnd ").Append("      ,tiOctetDEnd = :tiOctetDEnd ")
							.Append("      ,iDecimalEnd = :iDecimalEnd ")
							.Append("      ,vchUpdaterId = :vchUpdaterId ")
							.Append("      ,dtLastUpdate = :dtLastUpdate ")
							.Append(" WHERE iIpAddressId = :iIpAddressId ")
							.ToString();
						ISQLQuery query = uow.Session.CreateSQLQuery(sql);
						query.SetParameter("tiOctetCEnd", pair.Key.OctetCEnd);
						query.SetParameter("tiOctetDEnd", pair.Key.OctetDEnd);
						query.SetParameter("iDecimalEnd", pair.Key.IpNumberEnd);
						query.SetParameter("iIpAddressId", pair.Key.Id);
						query.SetParameter("vchUpdaterId", "MinimizeIpRangesTask");
						query.SetParameter("dtLastUpdate", DateTime.Now);
						query.ExecuteUpdate();
					}
					else
					{
						string sql2 = new StringBuilder().Append("UPDATE tIpAddressRange ").Append("   SET tiRecordStatus = :tiRecordStatus ").Append("      ,vchUpdaterId = :vchUpdaterId ")
							.Append("      ,dtLastUpdate = :dtLastUpdate ")
							.Append(" WHERE iIpAddressId = :iIpAddressId ")
							.ToString();
						ISQLQuery query2 = uow.Session.CreateSQLQuery(sql2);
						query2.SetParameter("tiRecordStatus", 0);
						query2.SetParameter("iIpAddressId", pair.Key.Id);
						query2.SetParameter("vchUpdaterId", "MinimizeIpRangesTask");
						query2.SetParameter("dtLastUpdate", DateTime.Now);
						query2.ExecuteUpdate();
					}
				}
			}
			catch (Exception ex)
			{
				R2UtilitiesBase.Log.ErrorFormat(ex.Message, ex);
			}
		}
	}
}
