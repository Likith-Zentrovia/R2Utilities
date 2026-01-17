using System;
using System.Collections.Generic;

namespace R2Utilities.DataAccess.Terms;

public class DiseaseSynonyms : Dictionary<Tuple<string, int>, DiseaseSynonym>
{
	public DiseaseSynonyms(IEnumerable<DiseaseSynonym> diseaseSynonyms)
	{
		foreach (DiseaseSynonym diseaseSynonym in diseaseSynonyms)
		{
			Add(Key(diseaseSynonym.Synonym, diseaseSynonym.DiseaseNameId), diseaseSynonym);
		}
	}

	public DiseaseSynonym Find(string synonym, int diseaseNameId)
	{
		Tuple<string, int> key = Key(synonym, diseaseNameId);
		return ContainsKey(key) ? base[key] : null;
	}

	private static Tuple<string, int> Key(string synonym, int diseaseNameId)
	{
		return new Tuple<string, int>(synonym, diseaseNameId);
	}
}
