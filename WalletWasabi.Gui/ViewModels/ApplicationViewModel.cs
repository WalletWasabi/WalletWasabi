using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.ViewModels
{
	public class ApplicationViewModel : ViewModelBase
	{
		public ApplicationViewModel()
		{
			AboutCommand = ReactiveCommand.Create(() =>
				IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel()));

			_ = AboutCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ReactiveCommand<Unit, Unit> AboutCommand { get; }
	}
}
