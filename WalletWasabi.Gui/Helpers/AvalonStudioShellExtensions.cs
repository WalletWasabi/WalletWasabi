using AvalonStudio.Documents;
using AvalonStudio.Shell;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using AvalonStudio.Extensibility;
using Avalonia.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using AvalonStudio.Commands;
using System.Reactive;

namespace AvalonStudio.Shell
{
	public static class AvalonStudioShellExtensions
	{
		// Replacement of avalonia's https://github.com/VitalElement/AvalonStudio.Shell/blob/eaa708e6c1428afd142e364dc6dbbd7ff5ba69dc/src/AvalonStudio.Shell.Extensibility/Shell/IShellExtensions.cs
		// because we want to use IoC to create T
		public static T GetOrCreateByType<T>(this IShell me) where T : IDocumentTabViewModel
		{
			return me.GetOrCreate<T>(() => IoC.Get<T>());
		}

		public static ReactiveCommand<Unit, Unit> GetReactiveCommand(this CommandDefinition cmd)
		{
			return cmd.Command as ReactiveCommand<Unit, Unit>;
		}
	}
}
