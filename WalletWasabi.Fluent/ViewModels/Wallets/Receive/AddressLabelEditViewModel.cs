using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	[NavigationMetaData(Title = "Edit Labels")]
	public partial class AddressLabelEditViewModel : RoutableViewModel
	{
		[AutoNotify] private ObservableCollection<string> _labels;
		[AutoNotify] private SmartLabel? _finalLabel;

		public AddressLabelEditViewModel(ReceiveAddressesViewModel owner, HdPubKey hdPubKey, KeyManager keyManager)
		{
			_labels = new(hdPubKey.Label);

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			Labels
				.WhenAnyValue(x => x.Count)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => { FinalLabel = new SmartLabel(Labels); });

			var canExecute = this.WhenAnyValue(x => x.FinalLabel).Select(x => x is {IsEmpty: false});

			NextCommand = ReactiveCommand.Create(() =>
			{
				hdPubKey.SetLabel(new SmartLabel(Labels), kmToFile: keyManager);
				owner.InitializeAddresses();
				Navigate().Back();
			}, canExecute);
		}
	}
}
