using ReactiveUI;
using System.IO;
using System.Reactive;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(string path) : base (Path.GetFileNameWithoutExtension(path))
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{

			});
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
