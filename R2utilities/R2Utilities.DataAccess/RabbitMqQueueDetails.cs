namespace R2Utilities.DataAccess;

public class RabbitMqQueueDetails
{
	public int memory { get; set; }

	public MessageStats message_stats { get; set; }

	public int messages { get; set; }

	public MessagesDetails messages_details { get; set; }

	public int messages_ready { get; set; }

	public MessagesReadyDetails messages_ready_details { get; set; }

	public int messages_unacknowledged { get; set; }

	public MessagesUnacknowledgedDetails messages_unacknowledged_details { get; set; }

	public string idle_since { get; set; }

	public object consumer_utilisation { get; set; }

	public object policy { get; set; }

	public object exclusive_consumer_tag { get; set; }

	public int consumers { get; set; }

	public object recoverable_slaves { get; set; }

	public string state { get; set; }

	public int messages_ram { get; set; }

	public int messages_ready_ram { get; set; }

	public int messages_unacknowledged_ram { get; set; }

	public int messages_persistent { get; set; }

	public int message_bytes { get; set; }

	public int message_bytes_ready { get; set; }

	public int message_bytes_unacknowledged { get; set; }

	public int message_bytes_ram { get; set; }

	public int message_bytes_persistent { get; set; }

	public object head_message_timestamp { get; set; }

	public int disk_reads { get; set; }

	public int disk_writes { get; set; }

	public BackingQueueStatus backing_queue_status { get; set; }

	public string name { get; set; }

	public string vhost { get; set; }

	public bool durable { get; set; }

	public bool auto_delete { get; set; }

	public bool exclusive { get; set; }

	public string node { get; set; }
}
