using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Mobile.Presentation;

/// <summary>Rendering contract only. Validation, balances, fees and commands remain wallet-owned.</summary>
public interface IMobileSendPresentation : INotifyPropertyChanged, INotifyDataErrorInfo
{
	string Caption { get; }
	string To { get; set; }
	decimal? AmountBtc { get; set; }
	decimal ExchangeRate { get; }
	bool ConversionReversed { get; set; }
	Amount? BalanceLatest { get; }
	bool IsFixedAddress { get; }
	bool IsFixedAmount { get; }
	bool IsBusy { get; }
	bool IsNotInDonationWorkflow { get; }
	bool IsQrButtonVisible { get; }
	bool IsPrimarySubtractFee { get; }
	bool DisplaySilentPaymentInfo { get; }
	bool IsPayJoin { get; }
	bool IsPayToMany { get; }
	string DefaultLabel { get; }
	ObservableCollection<string> Labels { get; }
	ObservableCollection<string> TopSuggestions { get; }
	ObservableCollection<string> Suggestions { get; }
	bool IsCurrentTextValid { get; set; }
	bool ForceAdd { get; set; }
	IEnumerable AdditionalRecipients { get; }
	MobileSendFeeSelection FeeSelection { get; }
	ICommand PasteCommand { get; }
	ICommand QrCommand { get; }
	ICommand InsertMaxCommand { get; }
	ICommand AddRecipientCommand { get; }
}
