using System.IO;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Io;

public static class SafeFile
{
	public static void WriteAllText(string filePath, string text, Encoding encoding)
	{
		var newFilePath = filePath + ".new";
		var oldFilePath = filePath + ".old";
		IoHelpers.EnsureContainingDirectoryExists(newFilePath);

		File.WriteAllText(newFilePath, text, encoding);
		if (File.Exists(filePath))
		{
			if (File.Exists(oldFilePath))
			{
				File.Delete(oldFilePath);
			}

			File.Move(filePath, oldFilePath);
		}

		File.Move(newFilePath, filePath);

		if (File.Exists(oldFilePath))
		{
			File.Delete(oldFilePath);
		}
	}

	public static string ReadAllText(string filePath, Encoding encoding)
	{
		var newFilePath = filePath + ".new";
		var oldFilePath = filePath + ".old";
		var path =
			(File.Exists(oldFilePath), File.Exists(filePath), File.Exists(newFilePath)) switch
			{
				// If foo.data and foo.data.new exist, load foo.data; foo.data.new may be broken (e.g. power off during write).
				(_, true, true) => filePath,
				// If foo.data.old and foo.data.new exist, both should be valid, but something died very shortly afterwards
				// - you may want to load the foo.data.old version anyway
				(true, _, true) => oldFilePath,
				// If foo.data and foo.data.old exist, then foo.data should be fine, but again something went wrong, or possibly the file could not be deleted.
				(_, true, _) => filePath,
				_ => throw new InvalidOperationException($"No safe version was found for {filePath}")
			};

		return File.ReadAllText(path, encoding);
	}
}
