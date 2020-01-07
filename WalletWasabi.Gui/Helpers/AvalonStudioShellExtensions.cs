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
		public static ReactiveCommand<Unit, Unit> GetReactiveCommand(this CommandDefinition cmd)
		{
			return cmd.Command as ReactiveCommand<Unit, Unit>;
		}
	}
}
