namespace R2Utilities.DataAccess;

public class MessageStats
{
	public int ack { get; set; }

	public AckDetails ack_details { get; set; }

	public int deliver { get; set; }

	public DeliverDetails deliver_details { get; set; }

	public int deliver_get { get; set; }

	public DeliverGetDetails deliver_get_details { get; set; }

	public int publish { get; set; }

	public PublishDetails publish_details { get; set; }
}
