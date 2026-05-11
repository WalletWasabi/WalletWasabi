using System.IO;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Io;

public static class SafeFile
{
	extension(File)
	{
		public static void SafelyWriteAllText(string filePath, string text, Encoding encoding)
		{
			Write(filePath, path => File.WriteAllText(path, text, encoding));
		}

		public static string SafelyReadAllText(string filePath, Encoding encoding)
		{
			return Read(filePath, path => File.ReadAllText(path, encoding));
		}

		public static void SafelyWriteAllBytes(string filePath, byte[] content)
		{
			Write(filePath, path => File.WriteAllBytes(path, content));
		}

		public static byte[] SafelyReadAllBytes(string filePath)
		{
			return Read(filePath, File.ReadAllBytes);
		}

		private static void Write(string filePath, Action<string> write)
		{
			var newFilePath = filePath + ".new";
			var oldFilePath = filePath + ".old";
			IoHelpers.EnsureContainingDirectoryExists(newFilePath);

			write(newFilePath);
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

		private static T Read<T>(string filePath, Func<string, T> read)
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

			return read(path);
		}
	}
}
