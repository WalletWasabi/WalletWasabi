using AvalonStudio.Documents;
using AvalonStudio.Shell;
using System;
using System.IO;
using System.Linq;

namespace WalletWasabi.Gui
{
	public static class Utils
	{
		public static string GetNextWalletName()
		{
			return "Foo";

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

		public static T GetOrCreate<T>(this IShell me) where T : IDocumentTabViewModel, new()
		{
			T document = default(T);
			IDocumentTabViewModel doc = me.Documents.FirstOrDefault(x => x is T);
			if (doc != default)
			{
				document = (T)doc;
				me.SelectedDocument = doc;
			}
			else
			{
				document = new T();
				me.AddDocument(document);
			}
			return document;
		}
	}
}
