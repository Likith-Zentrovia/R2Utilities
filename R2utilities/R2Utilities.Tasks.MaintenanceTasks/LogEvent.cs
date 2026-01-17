using System;
using R2Library.Data.ADO.Core;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class LogEvent : FactoryBase
{
	public DateTime Timestamp { get; set; }

	public string SessionId { get; set; }

	public string RequestId { get; set; }

	public string Level { get; set; }

	public string Server { get; set; }

	public int SendTime { get; set; }

	public string Url { get; set; }

	public bool Start { get; set; }

	public string ExceptionHash { get; set; }

	public LogEvent(string data, int sendTime, string server)
	{
		SendTime = sendTime;
		Server = server;
		string[] parts = data.Split(' ');
		string[] dateParts = parts[0].Split('-');
		string[] timeParts = parts[1].Split(':');
		string[] secondParts = timeParts[2].Split(',');
		Timestamp = new DateTime(int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]), int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(secondParts[0]), int.Parse(secondParts[1]));
		Level = parts[3];
		string[] pair = parts[4].Replace("[", "").Replace("]", "").Split('/');
		if (pair.Length == 1)
		{
			RequestId = pair[0];
			return;
		}
		SessionId = pair[0];
		RequestId = pair[1];
	}

	public LogEvent(string data, string url, string server, bool start)
	{
		Url = url;
		Server = server;
		Start = start;
		string[] parts = data.Split(' ');
		string[] dateParts = parts[0].Split('-');
		string[] timeParts = parts[1].Split(':');
		string[] secondParts = timeParts[2].Split(',');
		Timestamp = new DateTime(int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]), int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(secondParts[0]), int.Parse(secondParts[1]));
		Level = parts[3];
		string[] pair = parts[4].Replace("[", "").Replace("]", "").Split('/');
		if (pair.Length == 1)
		{
			RequestId = pair[0];
			return;
		}
		SessionId = pair[0];
		RequestId = pair[1];
	}

	public LogEvent(string data, string exceptionHash, string server)
	{
		Server = server;
		ExceptionHash = exceptionHash;
		string[] parts = data.Split(' ');
		string[] dateParts = parts[0].Split('-');
		string[] timeParts = parts[1].Split(':');
		string[] secondParts = timeParts[2].Split(',');
		Timestamp = new DateTime(int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]), int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(secondParts[0]), int.Parse(secondParts[1]));
		Level = parts[3];
		string[] pair = parts[4].Replace("[", "").Replace("]", "").Split('/');
		if (pair.Length == 1)
		{
			RequestId = pair[0];
			return;
		}
		SessionId = pair[0];
		RequestId = pair[1];
	}
}
