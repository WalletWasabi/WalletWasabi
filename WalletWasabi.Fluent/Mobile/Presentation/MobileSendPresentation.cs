using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.Presentation;

/// <summary>View-owned event adapter; never constructs, signs, approves or broadcasts a transaction.</summary>
public sealed class MobileSendPresentation : ReactiveObject, IMobileSendPresentation, IDisposable
{
	private readonly SendViewModel _source;
	private SuggestionLabelsViewModel _labels;
	private bool _disposed;

	public MobileSendPresentation(SendViewModel source)
	{
		_source = source ?? throw new ArgumentNullException(nameof(source));
		_labels = source.SuggestionLabels;
		FeeSelection = source.CreateMobileFeeSelection();
		source.PropertyChanged += SourceChanged;
		source.ErrorsChanged += ValidationChanged;
		_labels.PropertyChanged += LabelsChanged;
	}

	public string Caption => _source.Caption;
	public string To { get => _source.To; set => _source.To = value; }
	public decimal? AmountBtc { get => _source.AmountBtc; set => _source.AmountBtc = value; }
	public decimal ExchangeRate => _source.ExchangeRate;
	public bool ConversionReversed { get => _source.ConversionReversed; set => _source.ConversionReversed = value; }
	public Amount? BalanceLatest => _source.BalanceLatest;
	public bool IsFixedAddress => _source.IsFixedAddress;
	public bool IsFixedAmount => _source.IsFixedAmount;
	public bool IsBusy => _source.IsBusy;
	public bool IsNotInDonationWorkflow => _source.IsNotInDonationWorkflow;
	public bool IsQrButtonVisible => _source.IsQrButtonVisible;
	public bool IsPrimarySubtractFee => _source.IsPrimarySubtractFee;
	public bool DisplaySilentPaymentInfo => _source.DisplaySilentPaymentInfo;
	public bool IsPayJoin => _source.IsPayJoin;
	public bool IsPayToMany => _source.IsPayToMany;
	public string DefaultLabel => _source.DefaultLabel;
	public ObservableCollection<string> Labels => _labels.Labels;
	public ObservableCollection<string> TopSuggestions => _labels.TopSuggestions;
	public ObservableCollection<string> Suggestions => _labels.Suggestions;
	public bool IsCurrentTextValid { get => _labels.IsCurrentTextValid; set => _labels.IsCurrentTextValid = value; }
	public bool ForceAdd { get => _labels.ForceAdd; set => _labels.ForceAdd = value; }
	public IEnumerable AdditionalRecipients => _source.AdditionalRecipients;
	public MobileSendFeeSelection FeeSelection { get; }
	public ICommand PasteCommand => _source.PasteCommand;
	public ICommand QrCommand => _source.QrCommand;
	public ICommand InsertMaxCommand => _source.InsertMaxCommand;
	public ICommand AddRecipientCommand => _source.AddRecipientCommand;
	public bool HasErrors => ((INotifyDataErrorInfo)_source).HasErrors;
	public IEnumerable GetErrors(string? propertyName) => ((INotifyDataErrorInfo)_source).GetErrors(propertyName);
	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

	private void SourceChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_disposed) return;
		if (e.PropertyName is nameof(SendViewModel.SuggestionLabels) or null or "")
		{
			_labels.PropertyChanged -= LabelsChanged;
			_labels = _source.SuggestionLabels;
			_labels.PropertyChanged += LabelsChanged;
			this.RaisePropertyChanged(string.Empty);
		}
		else this.RaisePropertyChanged(e.PropertyName);
	}

	private void LabelsChanged(object? sender, PropertyChangedEventArgs e) => this.RaisePropertyChanged(e.PropertyName);
	private void ValidationChanged(object? sender, DataErrorsChangedEventArgs e)
	{
		ErrorsChanged?.Invoke(this, e);
		this.RaisePropertyChanged(nameof(HasErrors));
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_source.PropertyChanged -= SourceChanged;
		_source.ErrorsChanged -= ValidationChanged;
		_labels.PropertyChanged -= LabelsChanged;
		FeeSelection.Dispose();
	}
}
