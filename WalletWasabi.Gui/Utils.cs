using AvalonStudio.Documents;
using AvalonStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WalletWasabi.Gui
{
	public static class Utils
	{
		public static string GetNextWalletName()
		{
			for (int i = 0; i < int.MaxValue; i++)
			{
				if (!File.Exists(Path.Combine(Global.WalletsDir, $"Wallet{i}.json")))
				{
					return $"Wallet{i}";
				}
			}

			throw new NotSupportedException("This is impossible.");
		}

		public static void AddOrSelectDocument<T>(this IShell me, IDocumentTabViewModel document)
		{
			IDocumentTabViewModel doc = me.Documents.FirstOrDefault(x => x is T);
			if (doc != default)
			{
				me.SelectedDocument = doc;
			}
			else
			{
				me.AddDocument(document);
			}
		}
	}
}
