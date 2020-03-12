using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(string path) : base(Path.GetFileNameWithoutExtension(path))
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				IoC.Get<IShell>().AddOrSelectDocument(() => new WalletManagerViewModel());
				var walletManagerViewModel = IoC.Get<IShell>().Documents.OfType<WalletManagerViewModel>().FirstOrDefault();
				walletManagerViewModel.SelectLoadWallet();
				var loadWalletViewModel = walletManagerViewModel.SelectedCategory as LoadWalletViewModel;
				await loadWalletViewModel.LoadAsync(Title);
			});
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
