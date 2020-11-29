using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AboutViewModel : RoutableViewModel
	{
		public AboutViewModel()
		{
			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			var interaction = new Interaction<object, object>();
			interaction.RegisterHandler(
				async x =>
					x.SetOutput(
						await new AboutAdvancedInfoViewModel().ShowDialogAsync()));

			AboutAdvancedInfoDialogCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var h = await interaction
						.Handle(null).ToTask();
				});

			OpenBrowserCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ICommand AboutAdvancedInfoDialogCommand { get; }

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		public Version ClientVersion => Constants.ClientVersion;

		public string ClearnetLink => "https://wasabiwallet.io/";

		public string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";

		public string StatusPageLink => "https://stats.uptimerobot.com/YQqGyUL8A7";

		public string UserSupportLink => "https://www.reddit.com/r/WasabiWallet/";

		public string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public string FAQLink => "https://docs.wasabiwallet.io/FAQ/";

		public string DocsLink => "https://docs.wasabiwallet.io/";

		public string License => "https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md";
	}
}