using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using R2Library.Data.ADO.Core;
using R2V2.Extensions;

namespace R2Utilities.DataAccess.Mesh;

public class MeshTerm : FactoryBase, IDataEntity
{
	private string _scopeNote;

	public string Term { get; set; }

	public string DescriptorName { get; set; }

	public string ScopeNote
	{
		get
		{
			return _scopeNote.IfNotNull((string s) => s.Trim());
		}
		set
		{
			_scopeNote = value;
		}
	}

	public string TreeNumber { get; set; }

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Term = GetStringValue(reader, "Term");
			DescriptorName = GetStringValue(reader, "DescriptorName");
			ScopeNote = GetStringValue(reader, "ScopeNote");
			TreeNumber = GetStringValue(reader, "TreeNumber");
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}

	public static string ParentTreeNumber(string treeNumber)
	{
		List<string> parts = treeNumber.Split('.').ToList();
		parts.RemoveAt(parts.Count - 1);
		string result = string.Join(".", parts);
		return (result != "") ? result : null;
	}
}
