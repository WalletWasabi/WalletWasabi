using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>Populated production review content; transactions are not constructed or signed by these fixtures.</summary>
public sealed class MobileReviewSurfaceTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var scenario in new[] { "ready", "batch", "warnings", "analysis", "payjoin", "pending", "busy" })
			foreach (var width in new[] { 320, 390, 430 })
				foreach (var dark in new[] { false, true }) yield return new object[] { scenario, width, dark };
	}

	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void ActualReviewContentRendersAmountsWarningsAndPendingStates(string scenario, int width, bool dark)
	{
		using var errors = new MobileScreenshot.BindingErrors();
		var surface = CreateSurface();
		surface.IsPayToMany = scenario == "batch";
		surface.IsPayJoin = scenario == "payjoin";
		surface.IsPrivacyAnalyzing = scenario == "analysis";
		surface.IsBusy = scenario == "busy";
		if (scenario == "warnings")
		{
			surface.IsMaxPrivacy = false;
			surface.Warnings = new PrivacyWarning[] { new InterlinksLabelsWarning(new LabelsArray("Salary", "Unrelated sender")), new CreatesChangeWarning() };
		}
		if (scenario == "pending") surface.Amount = null;
		var window = Host(surface, width, dark);
		try
		{
			window.Show(); Pump();
			MobileScreenshot.Capture(window, $"review-{scenario}-{width}-{(dark ? "dark" : "light")}",
				"transaction-review-" + scenario, "bound-review-content");
			Assert.Equal(scenario != "pending", surface.HasReview);
			Assert.Equal(scenario == "pending", surface.FindControl<Border>("PendingReview")!.IsVisible);
			Assert.Equal(scenario == "batch", surface.FindControl<ItemsControl>("ReviewRecipients")!.IsVisible);
			Assert.Equal(scenario == "payjoin", surface.FindControl<Border>("PayJoinNotice")!.IsVisible);
			Assert.Same(surface.Amount, surface.FindControl<AmountControl>("PaymentAmount")!.Amount);
			Assert.Same(surface.Fee, surface.FindControl<AmountControl>("PaymentFee")!.Amount);
			if (scenario is "pending" or "analysis" or "warnings") Assert.False(surface.FindControl<Border>("NoPrivacyWarnings")!.IsVisible);
			if (scenario == "busy") Assert.All(surface.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEffectivelyEnabled));
			MobileScreenshot.AssertNoHorizontalOverflow(window);
			Assert.Empty(errors.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void ReviewActionsAndSuggestionSelectionStayBoundToTheirOwner(bool dark)
	{
		using var errors = new MobileScreenshot.BindingErrors();
		var owner = new ReviewOwner();
		var surface = CreateSurface();
		var calls = new List<string>();
		surface.AdjustFeeCommand = new ActionCommand(() => calls.Add("fee"));
		surface.ChangeCoinsCommand = new ActionCommand(() => calls.Add("coins"));
		surface.UndoCommand = new ActionCommand(() => calls.Add("undo"));
		surface.CanUndo = true;
		surface.IsMaxPrivacy = false;
		var suggestion = new LabelManagementSuggestion();
		surface.Suggestions = new[] { suggestion };
		surface.Bind(MobileReviewSurface.SelectedSuggestionProperty, new Binding(nameof(owner.Selection)) { Source = owner, Mode = BindingMode.TwoWay });
		var window = Host(surface, 390, dark);
		try
		{
			window.Show(); Pump();
			Press(window, surface.FindControl<Button>("ReviewFeeAction")!);
			Press(window, surface.FindControl<Button>("ReviewCoinsAction")!);
			Press(window, surface.FindControl<Button>("ReviewUndoAction")!);
			Assert.Equal(new[] { "fee", "coins", "undo" }, calls);
			var list = surface.FindControl<ListBox>("ReviewSuggestions")!;
			list.SelectedItem = suggestion; Pump();
			Assert.Same(suggestion, owner.Selection);
			owner.Selection = null; Pump();
			Assert.Null(list.SelectedItem);
			var replacement = new Amount(Money.Satoshis(12345));
			surface.Amount = replacement; Pump();
			Assert.Same(replacement, surface.FindControl<AmountControl>("PaymentAmount")!.Amount);
			surface.IsPrivacyAnalyzing = true;
			surface.IsMaxPrivacy = true;
			Pump();
			Assert.False(surface.FindControl<Border>("NoPrivacyWarnings")!.IsVisible);
			Assert.False(list.IsEffectivelyEnabled);
			surface.Amount = null; Pump();
			Assert.False(surface.HasReview);
			Assert.True(surface.FindControl<Border>("PendingReview")!.IsVisible);
			Assert.Empty(errors.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void ReviewWithoutWalletDataDoesNotClaimPrivacyAnalysisIsComplete()
	{
		var surface = new MobileReviewSurface();
		Assert.False(surface.HasReview);
		Assert.True(surface.IsPrivacyAnalyzing);
		Assert.False(surface.IsMaxPrivacy);
	}

	private static MobileReviewSurface CreateSurface()
	{
		using var key = new NBitcoin.Key(Enumerable.Repeat((byte)0x62, 32).ToArray());
		var address = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest).ToString();
		var first = new RecipientSummaryViewModel(null!, address, new Amount(Money.Coins(0.01m)), new LabelsArray("Invoice 1042"));
		var second = new RecipientSummaryViewModel(null!, address, new Amount(Money.Coins(0.005m)), new LabelsArray("Second recipient"));
		return new MobileReviewSurface
		{
			Amount = new Amount(Money.Coins(0.015m)), Fee = new Amount(Money.Satoshis(1420)),
			FeeRate = new FeeRate(10m), ConfirmationTime = TimeSpan.FromMinutes(20),
			AddressText = address, Recipient = new LabelsArray("Invoice 1042"), Recipients = new[] { first, second },
			Warnings = Array.Empty<PrivacyWarning>(), Suggestions = Array.Empty<PrivacySuggestion>(),
			IsPrivacyAnalyzing = false, IsMaxPrivacy = true, IsFeeAdjustable = true
		};
	}

	private static Window Host(Control surface, int width, bool dark) => new()
	{
		Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
		RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
		Content = new MobilePage
		{
			Header = "Review transaction", ShowBack = true,
			Footer = new Button { Content = "Confirm", Classes = { "mobile-button", "primary" }, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch },
			Content = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
				Content = new Border { Padding = new Thickness(16, 8, 16, 16), Child = surface } }
		}
	};
	private static void Press(Window window, Control control)
	{
		control.BringIntoView(); Pump(); Assert.True(control.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Pump();
	}
	private static void Pump() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
	private sealed class ActionCommand(Action action) : ICommand
	{
		public event EventHandler? CanExecuteChanged { add { } remove { } }
		public bool CanExecute(object? parameter) => true;
		public void Execute(object? parameter) => action();
	}
	private sealed class ReviewOwner : ReactiveObject
	{
		private PrivacySuggestion? _selection;
		public PrivacySuggestion? Selection { get => _selection; set => this.RaiseAndSetIfChanged(ref _selection, value); }
	}
}
