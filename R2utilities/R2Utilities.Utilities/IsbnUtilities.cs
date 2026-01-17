using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Common.Logging;

namespace R2Utilities.Utilities;

public class IsbnUtilities
{
	protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);

	public static string ConvertIsbn(string isbn)
	{
		if (!IsValidateIsbn(isbn))
		{
			throw new IsbnException("Invalid ISBN = '" + isbn + "'", "InvalidIsbn");
		}
		string cleanedIsbn = CleanIsbn(isbn);
		if (cleanedIsbn.Length == 10)
		{
			return ConvertIsbn10ToIsbn13(isbn);
		}
		if (cleanedIsbn.Length == 13)
		{
			return ConvertIsbn13ToIsbn10(isbn);
		}
		Log.WarnFormat("Invalid ISBN: '{0}'", isbn);
		throw new IsbnException("Invalid ISBN length, ISBN = '" + isbn + "'", "InvalidIsbnLength");
	}

	public static string ConvertIsbn10ToIsbn13(string isbn10)
	{
		if (!IsValidateIsbn(isbn10))
		{
			throw new IsbnException("Invalid ISBN = '" + isbn10 + "'", "InvalidIsbn");
		}
		string cleanedIsbn10 = CleanIsbn(isbn10);
		if (cleanedIsbn10.Length == 10)
		{
			int result = 0;
			string isbn13 = "978" + isbn10.Substring(0, 9);
			for (int i = 0; i < isbn13.Length; i++)
			{
				result += int.Parse(isbn13[i].ToString()) * ((i % 2 == 0) ? 1 : 3);
			}
			isbn13 += 10 - result % 10;
			Log.DebugFormat("converted {0} --> {1}, new ISBN 13 is valid? {2}", isbn10, isbn13, IsValidateIsbn(isbn13));
			return isbn13;
		}
		Log.WarnFormat("Invalid ISBN 10: '{0}'", isbn10);
		throw new IsbnException("Invalid ISBN 10 length, ISBN = '" + isbn10 + "'", "InvalidIsbn10Length");
	}

	public static string ConvertIsbn13ToIsbn10(string isbn13)
	{
		if (!IsValidateIsbn(isbn13))
		{
			throw new IsbnException("Invalid ISBN = '" + isbn13 + "'", "InvalidIsbn");
		}
		string cleanedIsbn13 = CleanIsbn(isbn13);
		if (cleanedIsbn13.Length == 13)
		{
			int total = 0;
			for (int x = 0; x < 11; x++)
			{
				int factor = ((x % 2 == 0) ? 1 : 3);
				total += Convert.ToInt32(cleanedIsbn13[x]) * factor;
			}
			int checksum = Convert.ToInt32(cleanedIsbn13[12]);
			if ((10 - total % 10) % 10 != checksum)
			{
				throw new IsbnException($"Error converting ISBN 10 to ISBN 13, checksum error, checksum: {(10 - total % 10) % 10} != {checksum}");
			}
			if (!cleanedIsbn13.StartsWith("978"))
			{
				throw new IsbnException("Error converting ISBN 10 to ISBN 13, invalid ISBN 13 prefix, ISBN 13 " + cleanedIsbn13);
			}
			string isbn14 = cleanedIsbn13.Substring(3, 9);
			total = 0;
			for (int i = 0; i < 8; i++)
			{
				total += Convert.ToInt32(isbn14[i]) * (10 - i);
			}
			checksum = (11 - total % 11) % 11;
			isbn14 += ((checksum == 10) ? "X" : $"{checksum}");
			Log.DebugFormat("converted {0} --> {1}, new ISBN 10 is valid? {2}", isbn13, isbn14, IsValidateIsbn(isbn14));
			return isbn14;
		}
		Log.WarnFormat("Invalid ISBN 13: '{0}'", isbn13);
		throw new IsbnException("Invalid ISBN 13 length, ISBN = '" + isbn13 + "'", "InvalidIsbn13Length");
	}

	public static string CleanIsbn(string isbn)
	{
		if (string.IsNullOrEmpty(isbn))
		{
			throw new IsbnException("ISBN is null or empty", "NullorEmpty");
		}
		return isbn.Replace("-", string.Empty).Replace(" ", string.Empty);
	}

	public static bool IsValidateIsbn(string isbn)
	{
		if (string.IsNullOrEmpty(isbn))
		{
			throw new IsbnException("ISBN is null or empty", "NullorEmpty");
		}
		Regex isbnRegex = new Regex("^(97(8|9))?\\d{9}(\\d|X)$");
		return isbnRegex.IsMatch(isbn);
	}
}
