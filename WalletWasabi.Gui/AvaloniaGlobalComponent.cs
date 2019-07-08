using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;

namespace WalletWasabi.Gui
{
	[Export]
	[Shared]
	public class AvaloniaGlobalComponent
	{
		// Because we want to create the Global instance before running
		// Avalonia, we cannot use Avalonia's IoC to create our singleton as it will
		// create a new instance.
		public static Global AvaloniaInstance { get; set; }

		public Global Global => AvaloniaInstance;
	}
}
