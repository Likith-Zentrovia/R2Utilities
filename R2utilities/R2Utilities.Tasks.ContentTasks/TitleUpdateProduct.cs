using System;

namespace R2Utilities.Tasks.ContentTasks;

[Serializable]
public class TitleUpdateProduct
{
	public int ResourceId { get; set; }

	public string Isbn { get; set; }

	public string Title { get; set; }
}
