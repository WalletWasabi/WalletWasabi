using Avalonia.Threading;
using AvalonStudio.Commands;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace AvalonStudio.Shell
{
	public static class AvalonStudioShellExtensions
	{
		// Replacement of avalonia's https://github.com/VitalElement/AvalonStudio.Shell/blob/eaa708e6c1428afd142e364dc6dbbd7ff5ba69dc/src/AvalonStudio.Shell.Extensibility/Shell/IShellExtensions.cs
		// because we want to use IoC to create T
		public static T GetOrCreateByType<T>(this IShell me) where T : IDocumentTabViewModel
		{
			return me.GetOrCreate(() => IoC.Get<T>());
		}

		public static ReactiveCommand<Unit, Unit> GetReactiveCommand(this CommandDefinition cmd)
		{
			return cmd.Command as ReactiveCommand<Unit, Unit>;
		}
	}
}
