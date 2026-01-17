using System.Collections.Generic;
using R2V2.Core.Resource;

namespace R2Utilities.DataAccess;

public class OnixEisbnDuplidate
{
	public List<Resource> DuplicateResoruces { get; set; }

	public OnixEisbn OnixEisbn { get; set; }

	public OnixEisbnDuplidate(OnixEisbn onixEisbn, List<Resource> duplicateResources)
	{
		DuplicateResoruces = duplicateResources;
		OnixEisbn = onixEisbn;
	}
}
