using System.IO;
using System;
using System.Reflection;

namespace WalletWasabi
{
	public class GitInfo
	{
		public static string BuildCommitHash { get; }

		public static string BuildBranch { get; }

		static GitInfo()
		{
			var assembly = Assembly.GetExecutingAssembly();
			BuildCommitHash = GetStringFromResourceStream(assembly, "WalletWasabi.GitInfo.git-info-commit.txt");
			BuildBranch = GetStringFromResourceStream(assembly, "WalletWasabi.GitInfo.git-info-branch.txt");
		}

		private static string GetStringFromResourceStream(Assembly assembly, string resId)
		{
			try
			{
				using var reader = new StreamReader(assembly.GetManifestResourceStream(resId));
				return reader.ReadToEnd().Trim().Trim('\r').Trim('\n');
			}
			catch (Exception _)
			{
				return "";
			}
		}
	}
}
