using System;
using System.IO;
using System.Reflection;
using Common.Logging;
using ICSharpCode.SharpZipLib.Zip;

namespace R2Utilities.Utilities;

public static class FileCompression
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);

	public static void CompressDirectory(string directory)
	{
		try
		{
			string zipFileName = (directory.EndsWith("\\") ? directory.Substring(0, directory.Length - 1) : directory) + ".zip";
			string[] filenames = Directory.GetFiles(directory);
			using ZipOutputStream s = new ZipOutputStream(File.Create(zipFileName));
			s.SetLevel(9);
			byte[] buffer = new byte[4096];
			string[] array = filenames;
			foreach (string file in array)
			{
				ZipEntry entry = new ZipEntry(Path.GetFileName(file));
				entry.DateTime = DateTime.Now;
				s.PutNextEntry(entry);
				using FileStream fs = File.OpenRead(file);
				int sourceBytes;
				do
				{
					sourceBytes = fs.Read(buffer, 0, buffer.Length);
					s.Write(buffer, 0, sourceBytes);
				}
				while (sourceBytes > 0);
			}
			s.Finish();
			s.Close();
		}
		catch (Exception ex)
		{
			Log.Error(ex.Message, ex);
			throw;
		}
	}
}
