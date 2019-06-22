using AvalonStudio.Documents;
using AvalonStudio.Shell;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using AvalonStudio.Extensibility;

namespace System
{
	public static class AvaloniaExtensions
	{
		// Replacement of avalonia's https://github.com/VitalElement/AvalonStudio.Shell/blob/eaa708e6c1428afd142e364dc6dbbd7ff5ba69dc/src/AvalonStudio.Shell.Extensibility/Shell/IShellExtensions.cs
		// because we want to use IoC to create T
		public static T GetOrCreate<T>(this IShell me) where T : IDocumentTabViewModel
		{
			T document = default;

			IDocumentTabViewModel doc = me.Documents.FirstOrDefault(x => x is T);

			if (doc != default)
			{
				document = (T)doc;
				me.SelectedDocument = doc;
			}
			else
			{
				// document = new T();
				document = IoC.Get<T>();
				me.AddDocument(document);
			}

			return document;
		}
	}
}
