using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Legal
{
	public static class LegalDocuments
	{
		public const string FileName = "LegalDocuments.txt";
		public const string LegalFolderName = "Legal";
		public const string AssetsFoldername = "Assets";
		public static readonly string FilePath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), LegalFolderName, AssetsFoldername, FileName);
	}
}
