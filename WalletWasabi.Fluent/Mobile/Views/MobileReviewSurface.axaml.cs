using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>
/// Native review content with explicit wallet-owned inputs. It never recalculates a
/// payment, approves a suggestion, or signs: the host binds the existing view model's
/// amounts, analysis and commands. Populated headless tests use this same control.
/// </summary>
public sealed class MobileReviewSurface : UserControl
{
	public static readonly StyledProperty<Amount?> AmountProperty = AvaloniaProperty.Register<MobileReviewSurface, Amount?>(nameof(Amount), null);
	public static readonly StyledProperty<Amount?> FeeProperty = AvaloniaProperty.Register<MobileReviewSurface, Amount?>(nameof(Fee), null);
	public static readonly StyledProperty<FeeRate?> FeeRateProperty = AvaloniaProperty.Register<MobileReviewSurface, FeeRate?>(nameof(FeeRate), null);
	public static readonly StyledProperty<TimeSpan?> ConfirmationTimeProperty = AvaloniaProperty.Register<MobileReviewSurface, TimeSpan?>(nameof(ConfirmationTime), null);
	public static readonly StyledProperty<string?> AddressTextProperty = AvaloniaProperty.Register<MobileReviewSurface, string?>(nameof(AddressText), null);
	public static readonly StyledProperty<LabelsArray> RecipientProperty = AvaloniaProperty.Register<MobileReviewSurface, LabelsArray>(nameof(Recipient), LabelsArray.Empty);
	public static readonly StyledProperty<IReadOnlyList<RecipientSummaryViewModel>?> RecipientsProperty = AvaloniaProperty.Register<MobileReviewSurface, IReadOnlyList<RecipientSummaryViewModel>?>(nameof(Recipients), null);
	public static readonly StyledProperty<IEnumerable<PrivacyWarning>?> WarningsProperty = AvaloniaProperty.Register<MobileReviewSurface, IEnumerable<PrivacyWarning>?>(nameof(Warnings), null);
	public static readonly StyledProperty<IEnumerable<PrivacySuggestion>?> SuggestionsProperty = AvaloniaProperty.Register<MobileReviewSurface, IEnumerable<PrivacySuggestion>?>(nameof(Suggestions), null);
	public static readonly StyledProperty<PrivacySuggestion?> SelectedSuggestionProperty = AvaloniaProperty.Register<MobileReviewSurface, PrivacySuggestion?>(nameof(SelectedSuggestion), null, defaultBindingMode: BindingMode.TwoWay);
	public static readonly StyledProperty<bool> IsPayToManyProperty = AvaloniaProperty.Register<MobileReviewSurface, bool>(nameof(IsPayToMany), false);
	public static readonly StyledProperty<bool> IsPayJoinProperty = AvaloniaProperty.Register<MobileReviewSurface, bool>(nameof(IsPayJoin), false);
	public static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<MobileReviewSurface, bool>(nameof(IsBusy), false);
	public static readonly StyledProperty<bool> IsPrivacyAnalyzingProperty = AvaloniaProperty.Register<MobileReviewSurface, bool>(nameof(IsPrivacyAnalyzing), true);
	public static readonly StyledProperty<bool> IsMaxPrivacyProperty = AvaloniaProperty.Register<MobileReviewSurface, bool>(nameof(IsMaxPrivacy), false);
	public static readonly StyledProperty<bool> CanUndoProperty = AvaloniaProperty.Register<MobileReviewSurface, bool>(nameof(CanUndo), false);
	public static readonly StyledProperty<bool> IsFeeAdjustableProperty = AvaloniaProperty.Register<MobileReviewSurface, bool>(nameof(IsFeeAdjustable), false);
	public static readonly StyledProperty<ICommand?> AdjustFeeCommandProperty = AvaloniaProperty.Register<MobileReviewSurface, ICommand?>(nameof(AdjustFeeCommand), null);
	public static readonly StyledProperty<ICommand?> ChangeCoinsCommandProperty = AvaloniaProperty.Register<MobileReviewSurface, ICommand?>(nameof(ChangeCoinsCommand), null);
	public static readonly StyledProperty<ICommand?> UndoCommandProperty = AvaloniaProperty.Register<MobileReviewSurface, ICommand?>(nameof(UndoCommand), null);
	public static readonly DirectProperty<MobileReviewSurface, bool> HasReviewProperty =
		AvaloniaProperty.RegisterDirect<MobileReviewSurface, bool>(nameof(HasReview), x => x.HasReview);
	private bool _hasReview;

	public MobileReviewSurface()
	{
		AvaloniaXamlLoader.Load(this);
		this.FindControl<StackPanel>("ReviewRoot")!.DataContext = this;
	}
	public bool HasReview => _hasReview;
	public Amount? Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }
	public Amount? Fee { get => GetValue(FeeProperty); set => SetValue(FeeProperty, value); }
	public FeeRate? FeeRate { get => GetValue(FeeRateProperty); set => SetValue(FeeRateProperty, value); }
	public TimeSpan? ConfirmationTime { get => GetValue(ConfirmationTimeProperty); set => SetValue(ConfirmationTimeProperty, value); }
	public string? AddressText { get => GetValue(AddressTextProperty); set => SetValue(AddressTextProperty, value); }
	public LabelsArray Recipient { get => GetValue(RecipientProperty); set => SetValue(RecipientProperty, value); }
	public IReadOnlyList<RecipientSummaryViewModel>? Recipients { get => GetValue(RecipientsProperty); set => SetValue(RecipientsProperty, value); }
	public IEnumerable<PrivacyWarning>? Warnings { get => GetValue(WarningsProperty); set => SetValue(WarningsProperty, value); }
	public IEnumerable<PrivacySuggestion>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }
	public PrivacySuggestion? SelectedSuggestion { get => GetValue(SelectedSuggestionProperty); set => SetValue(SelectedSuggestionProperty, value); }
	public bool IsPayToMany { get => GetValue(IsPayToManyProperty); set => SetValue(IsPayToManyProperty, value); }
	public bool IsPayJoin { get => GetValue(IsPayJoinProperty); set => SetValue(IsPayJoinProperty, value); }
	public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
	public bool IsPrivacyAnalyzing { get => GetValue(IsPrivacyAnalyzingProperty); set => SetValue(IsPrivacyAnalyzingProperty, value); }
	public bool IsMaxPrivacy { get => GetValue(IsMaxPrivacyProperty); set => SetValue(IsMaxPrivacyProperty, value); }
	public bool CanUndo { get => GetValue(CanUndoProperty); set => SetValue(CanUndoProperty, value); }
	public bool IsFeeAdjustable { get => GetValue(IsFeeAdjustableProperty); set => SetValue(IsFeeAdjustableProperty, value); }
	public ICommand? AdjustFeeCommand { get => GetValue(AdjustFeeCommandProperty); set => SetValue(AdjustFeeCommandProperty, value); }
	public ICommand? ChangeCoinsCommand { get => GetValue(ChangeCoinsCommandProperty); set => SetValue(ChangeCoinsCommandProperty, value); }
	public ICommand? UndoCommand { get => GetValue(UndoCommandProperty); set => SetValue(UndoCommandProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == AmountProperty || change.Property == FeeProperty)
			SetAndRaise(HasReviewProperty, ref _hasReview, Amount is not null && Fee is not null);
	}
}
