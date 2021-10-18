using System;
using System.Collections.Generic;
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
		[AutoNotify] private bool _isCurrentTextValid;

		public AddressLabelEditViewModel(ReceiveAddressesViewModel owner, HdPubKey hdPubKey, KeyManager keyManager, HashSet<string> suggestions)
		{
			Suggestions = suggestions;
			_labels = new(hdPubKey.Label);

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			Labels
				.WhenAnyValue(x => x.Count)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => FinalLabel = new SmartLabel(Labels));

			var canExecute =
				this.WhenAnyValue(x => x.FinalLabel, x => x.IsCurrentTextValid)
					.Select(tup =>
					{
						var (finalLabel, isCurrentTextValid) = tup;
						return finalLabel is { IsEmpty: false } || isCurrentTextValid;
					});

			NextCommand = ReactiveCommand.Create(
				() =>
				{
					if (FinalLabel is null)
					{
						return;
					}

					hdPubKey.SetLabel(FinalLabel, kmToFile: keyManager);
					owner.InitializeAddresses();
					Navigate().Back();
				},
				canExecute);
		}

		public HashSet<string> Suggestions { get; }
	}
}
