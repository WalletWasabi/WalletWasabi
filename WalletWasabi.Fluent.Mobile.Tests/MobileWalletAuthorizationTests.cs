using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Mobile.Views;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>
/// Exercises the real password verifier through the real authorization command and
/// compiled mobile view. The wallet is in-memory regtest, never started, funded or
/// signed. Unused services are deliberately absent: this is not a network/signing test.
/// </summary>
public sealed class MobileWalletAuthorizationTests
{
	[AvaloniaTheory]
	[InlineData(320, false)]
	[InlineData(320, true)]
	[InlineData(390, false)]
	[InlineData(390, true)]
	public async Task RealWalletRejectsWrongPassphraseAndAuthorizesOnlyAfterSuccessfulRetry(int width, bool dark)
	{
		const string passphrase = "Headless-regtest-only-42";
		var keys = KeyManager.CreateNew(out _, passphrase, Network.RegTest);
		using var wallet = Wallet.CreateFactory(Network.RegTest, null!, null!, new FilterHeaderChain(), null!,
			new ServiceConfiguration(Money.Satoshis(546)),
			(_, _) => throw new InvalidOperationException("Authorization must not download blocks."),
			new EventBus(), null!)(keys);
		var model = new PasswordAuthDialogViewModel(null!, new AuthenticationOnlyWallet(new WalletAuthModel(wallet)));
		var result = model.GetDialogResultAsync();
		var command = Assert.IsType<ReactiveCommand<Unit, Unit>>(model.NextCommand);
		using var bindings = new MobileScreenshot.BindingErrors();
		var view = new MobilePasswordAuthorizationView { DataContext = model };
		var window = new Window
		{
			Width = width, Height = 844, SystemDecorations = SystemDecorations.None, Content = view,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
		};
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			var input = view.FindControl<TextBox>("PassphraseInput")!;
			var button = view.FindControl<Button>("AuthorizePassword")!;
			Assert.Same(command, button.Command);
			MobileScreenshot.Capture(window, $"authorization-regtest-ready-{width}-{(dark ? "dark" : "light")}",
				"wallet-passphrase-ready", "bound-regtest-authorization");

			input.Text = "Deliberately-incorrect";
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(input.Text, model.Password);
			await PressAndCompleteAsync(window, button, command);
			Assert.False(result.IsCompleted);
			Assert.True(model.HasAuthorizationFailed);
			Assert.True(view.FindControl<TextBlock>("AuthorizationError")!.IsVisible);
			Assert.Empty(model.Password);
			Assert.True(string.IsNullOrEmpty(input.Text));
			MobileScreenshot.Capture(window, $"authorization-regtest-rejected-{width}-{(dark ? "dark" : "light")}",
				"wallet-passphrase-rejected", "bound-regtest-authorization");

			input.Text = passphrase;
			Dispatcher.UIThread.RunJobs();
			await PressAndCompleteAsync(window, button, command);
			var authorized = await result.WaitAsync(TimeSpan.FromSeconds(30));
			Assert.Equal(DialogResultKind.Normal, authorized.Kind);
			Assert.True(authorized.Result);
			Assert.False(model.HasAuthorizationFailed);
			Assert.Empty(model.Password);
			Assert.True(string.IsNullOrEmpty(input.Text));
			Assert.False(wallet.IsLoggedIn);
			Assert.False(wallet.Loaded);
			Assert.Null(wallet.KeyChain);
			Assert.Empty(wallet.Coins);
			MobileScreenshot.AssertNoHorizontalOverflow(window);
			Assert.Empty(bindings.Errors);
		}
		finally
		{
			window.Close();
			model.IsDialogOpen = false;
			command.Dispose();
			(model.BackCommand as IDisposable)?.Dispose();
			(model.CancelCommand as IDisposable)?.Dispose();
			wallet.WalletFilterProcessor.Dispose();
		}
	}

	private static async Task PressAndCompleteAsync(Window window, Button button, ReactiveCommand<Unit, Unit> command)
	{
		var completed = command.Take(1).ToTask();
		button.BringIntoView(); Dispatcher.UIThread.RunJobs();
		Assert.True(button.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
		await completed.WaitAsync(TimeSpan.FromSeconds(30));
		Dispatcher.UIThread.RunJobs();
	}

	/// <summary>Only unrelated wallet capabilities are stubbed; Auth is the production verifier.</summary>
	private sealed class AuthenticationOnlyWallet(WalletAuthModel auth) : IWalletModel
	{
		public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
		public WalletAuthModel Auth => auth;
		public bool IsHardwareWallet => false;
		public bool IsWatchOnlyWallet => false;
		public bool IsLoggedIn { get => false; set => throw Unexpected(); }
		public bool IsLoaded { get => false; set => throw Unexpected(); }
		public bool IsSelected { get => false; set => throw Unexpected(); }
		public IObservable<bool> IsCoinjoinRunning => throw Unexpected();
		public IObservable<bool> IsCoinjoinStarted => throw Unexpected();
		public bool IsCoinJoinEnabled => throw Unexpected();
		public AddressesModel Addresses => throw Unexpected();
		public WalletId Id => throw Unexpected();
		public string Name => throw Unexpected();
		public Network Network => Network.RegTest;
		public IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes => throw Unexpected();
		public bool SeveralReceivingScriptTypes => throw Unexpected();
		public WalletTransactionsModel Transactions => throw Unexpected();
		public IObservable<Amount> Balances => throw Unexpected();
		public IObservable<bool> HasBalance => throw Unexpected();
		public WalletCoinsModel Coins => throw Unexpected();
		public WalletLoadWorkflow Loader => throw Unexpected();
		public WalletSettingsModel Settings => throw Unexpected();
		public WalletPrivacyModel Privacy => throw Unexpected();
		public WalletCoinjoinModel? Coinjoin => throw Unexpected();
		public IObservable<bool> Loaded => throw Unexpected();
		public AmountProvider AmountProvider => throw Unexpected();
		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent) => throw Unexpected();
		public IWalletStatsModel GetWalletStats() => throw Unexpected();
		public WalletInfoModel GetWalletInfo() => throw Unexpected();
		public PrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendFlow) => throw Unexpected();
		public void Rename(string newWalletName) => throw Unexpected();
		private static InvalidOperationException Unexpected() => new("Passphrase verification accessed an unrelated wallet capability.");
	}
}
