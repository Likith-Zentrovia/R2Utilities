using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using R2Library.Data.ADO.R2Utility;
using R2V2.Core.RequestLogger;

namespace R2Utilities.Tasks.Testing;

public class RequestLoggingTestTask : TaskBase
{
	private readonly RequestLoggerService _requestLoggerService;

	private int _delay;

	private int _requestsPerThread;

	private int _threadCount;

	public RequestLoggingTestTask(RequestLoggerService requestLoggerService)
		: base("RequestLoggingTestTask", "-RequestLoggingTestTask", "20", TaskGroup.DiagnosticsMaintenance, "Task will send many test request logger messages", enabled: true)
	{
		_requestLoggerService = requestLoggerService;
	}

	public override void Run()
	{
		base.TaskResult.Information = base.TaskDescription;
		TaskResultStep step = new TaskResultStep
		{
			Name = "SendRequestLoggerMessages",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		try
		{
			int.TryParse(GetArgument("threadCount") ?? "3", out _threadCount);
			int.TryParse(GetArgument("requestsPerThread") ?? "10", out _requestsPerThread);
			int.TryParse(GetArgument("delay") ?? "1", out _delay);
			Dictionary<string, Thread> threads = new Dictionary<string, Thread>();
			for (int i = 0; i < _threadCount; i++)
			{
				string threadName = $"Thread{i:0000#}";
				Thread thread = new Thread(SendRequestLoggerMessages)
				{
					Name = threadName
				};
				thread.Start();
				threads.Add(threadName, thread);
			}
			while (threads.Select((KeyValuePair<string, Thread> keyValuePair) => keyValuePair.Value).Count((Thread thread2) => thread2.IsAlive) != 0)
			{
				Thread.Sleep(1000);
			}
			stopwatch.Stop();
			step.CompletedSuccessfully = true;
			step.Results = $"_threadCount: {_threadCount}, _requestsPerThread: {_requestsPerThread}, _delay: {_delay}, ElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}";
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

	public void SendRequestLoggerMessages()
	{
		ApplicationSession applicationSession = new ApplicationSession
		{
			HitCount = 0,
			Referrer = "",
			SessionId = "n/a",
			SessionLastRequestTime = DateTime.Now,
			SessionStartTime = DateTime.Now
		};
		string threadName = Thread.CurrentThread.Name;
		for (int i = 0; i < _requestsPerThread; i++)
		{
			applicationSession.HitCount++;
			applicationSession.SessionLastRequestTime = DateTime.Now;
			RequestData requestData = new RequestData
			{
				RequestTimestamp = DateTime.Now,
				InstitutionId = 1,
				UserId = 0,
				Url = $"/RequestLoggerTesting/{threadName}/{i}",
				IpAddress = new IpAddress("127.0.0.1"),
				Session = applicationSession,
				RequestId = Guid.NewGuid().ToString(),
				SearchRequest = null,
				Referrer = "",
				CountryCode = "US",
				ServerNumber = 99
			};
			if (_delay > 0)
			{
				Thread.Sleep(_delay);
			}
			double duration = DateTime.Now.Subtract(requestData.RequestTimestamp).TotalMilliseconds;
			requestData.RequestDuration = Convert.ToInt32(duration);
			if (!_requestLoggerService.WriteRequestDataToMessageQueue(requestData))
			{
				R2UtilitiesBase.Log.ErrorFormat("Error writing request to message queue: {0}", requestData.ToJsonString());
			}
		}
	}
}
