using System;
using System.Collections.Generic;
using R2Utilities.DataAccess.Mesh;

namespace R2Utilities.DataAccess.Terms;

public class DiseaseNames : Dictionary<Tuple<string, string>, DiseaseName>
{
	public DiseaseNames(IEnumerable<DiseaseName> diseaseNames)
	{
		foreach (DiseaseName diseaseName in diseaseNames)
		{
			Add(Key(diseaseName.Name, diseaseName.RelationName), diseaseName);
		}
	}

	public DiseaseName Find(MeshTerm meshTerm)
	{
		Tuple<string, string> key = Key(meshTerm.DescriptorName, meshTerm.TreeNumber);
		return ContainsKey(key) ? base[key] : null;
	}

	private static Tuple<string, string> Key(string name, string relationName)
	{
		return new Tuple<string, string>(name, relationName);
	}
}
