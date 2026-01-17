using System.Reflection;
using Common.Logging;
using R2Library.Data.ADO.R2.DataServices;
using R2Utilities.Tasks.ContentTasks;
using R2V2.Infrastructure.Compression;

namespace R2Utilities;

public class R2UtilitiesBase : DataServiceBase
{
	protected new static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);

	public void CompressResourceContentDirectory(ResourceContentDirectory resourceContentDirectory, string zipFileName)
	{
		string folderName = resourceContentDirectory.ResourceDirectory.FullName;
		if (!resourceContentDirectory.ResourceDirectory.Exists)
		{
			Log.InfoFormat("-- Directory does not exist: {0}", resourceContentDirectory.ResourceDirectory.FullName);
		}
		else
		{
			ZipHelper.CompressDirectory(folderName, zipFileName, resourceContentDirectory.ContentType);
		}
	}
}
