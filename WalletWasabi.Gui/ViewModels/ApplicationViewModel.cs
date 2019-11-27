using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Reactive;
using System.Text;
using WalletWasabi.Gui.Tabs;

namespace WalletWasabi.Gui.ViewModels
{
	public class ApplicationViewModel : ViewModelBase
	{
		public ApplicationViewModel(Global global)
		{
			AboutCommand = ReactiveCommand.Create(() =>
			{
				IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel(global));
			});
		}

		public ReactiveCommand<Unit, Unit> AboutCommand { get; }
	}
}
