using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>
/// Bound UI tests for local view-model behavior, not wallet creation or restoration.
/// Null contexts are deliberate: the tested constructors and commands only validate or
/// complete local dialog results. Any accidental navigation/service access fails the test.
/// No mnemonic is generated, no recovery words are submitted, and no wallet is started.
/// </summary>
public sealed class MobileBackupInteractionTests
{
	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void PassphraseBindingsPreserveOriginalValidationAndDialogResult(bool dark)
	{
		var model = new CreatePasswordDialogViewModel(null!, "Test passphrase", "Synthetic test only", enableEmpty: false);
		var result = model.GetDialogResultAsync();
		var view = new MobileCreatePasswordView { DataContext = model };
		var window = CreateWindow(view, dark);
		try
		{
			window.Show(); Pump();
			var password = view.FindControl<TextBox>("NewPassphrase")!;
			var confirmation = view.FindControl<TextBox>("ConfirmPassphrase")!;
			var action = view.FindControl<Button>("PrimaryAction")!;
			Assert.Same(model.NextCommand, action.Command);
			Assert.False(model.NextCommand!.CanExecute(null));
			Assert.False(action.IsEffectivelyEnabled);
			foreach (var input in new[] { password, confirmation })
			{
				Assert.Equal('●', input.PasswordChar);
				Assert.False(input.RevealPassword);
				Assert.False(input.IsUndoEnabled);
				Assert.True(TextInputOptions.GetIsSensitive(input));
				Assert.Equal(false, TextInputOptions.GetShowSuggestions(input));
				Assert.False(TextInputOptions.GetAutoCapitalization(input));
			}
			password.Text = "Synthetic-Only-42";
			confirmation.Text = "different";
			Pump();
			Assert.Equal(password.Text, model.Password);
			Assert.Equal(confirmation.Text, model.ConfirmPassword);
			Assert.False(model.NextCommand.CanExecute(null));
			confirmation.Text = "Synthetic-Only-42";
			Pump();
			Assert.True(action.IsEffectivelyEnabled);
			Press(window, action);
			Assert.True(result.IsCompletedSuccessfully);
			Assert.Equal(DialogResultKind.Normal, result.Result.Kind);
			Assert.Equal("Synthetic-Only-42", result.Result.Result);
			Assert.Equal(string.Empty, model.Password);
			Assert.Equal(string.Empty, model.ConfirmPassword);
		}
		finally { window.Close(); DisposeCommands(model); }
	}

	[AvaloniaFact]
	public void CancelDoesNotReturnTheEnteredPassphrase()
	{
		var model = new CreatePasswordDialogViewModel(null!, "Test passphrase", enableEmpty: false);
		var result = model.GetDialogResultAsync();
		var view = new MobileCreatePasswordView { DataContext = model };
		var window = CreateWindow(view);
		try
		{
			window.Show(); Pump();
			view.FindControl<TextBox>("NewPassphrase")!.Text = "Synthetic-cancelled";
			Pump();
			var cancel = view.GetVisualDescendants().OfType<Button>().Single(x => ReferenceEquals(x.Command, model.CancelCommand));
			Press(window, cancel);
			Assert.True(result.IsCompletedSuccessfully);
			Assert.Equal(DialogResultKind.Cancel, result.Result.Kind);
			Assert.Null(result.Result.Result);
			Assert.Equal(string.Empty, model.Password);
		}
		finally { window.Close(); DisposeCommands(model); }
	}

	[AvaloniaFact]
	public void ReplacingThePassphraseModelClearsOldEditableState()
	{
		var first = new CreatePasswordDialogViewModel(null!, "First");
		var second = new CreatePasswordDialogViewModel(null!, "Second");
		var view = new MobileCreatePasswordView { DataContext = first };
		var window = CreateWindow(view);
		try
		{
			window.Show(); Pump();
			first.Password = "Synthetic-first"; first.ConfirmPassword = "Synthetic-first";
			view.DataContext = second;
			Pump();
			Assert.Equal(string.Empty, first.Password);
			Assert.Equal(string.Empty, first.ConfirmPassword);
			second.Password = "Synthetic-second"; second.ConfirmPassword = "Synthetic-second";
			window.Content = null; Pump();
			Assert.Equal(string.Empty, second.Password);
			Assert.Equal(string.Empty, second.ConfirmPassword);
		}
		finally { window.Close(); DisposeCommands(first); DisposeCommands(second); }
	}

	[AvaloniaFact]
	public void BoundErrorScreenReturnsTheOriginalDialogResult()
	{
		var model = new ShowErrorDialogViewModel(null!, "Synthetic failure detail", "Test error", "No services involved");
		var result = model.GetDialogResultAsync();
		var view = new MobileErrorView { DataContext = model };
		var window = CreateWindow(view);
		try
		{
			window.Show(); Pump();
			Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Synthetic failure detail");
			Press(window, view.FindControl<Button>("PrimaryAction")!);
			Assert.True(result.IsCompletedSuccessfully);
			Assert.Equal(DialogResultKind.Normal, result.Result.Kind);
		}
		finally { window.Close(); DisposeCommands(model); }
	}

	[AvaloniaFact]
	public void BackupRevealIsResetByAncestorVisibilityContextAndDetachment()
	{
		var panel = new MobileSecretPanel { Header = "Synthetic backup", Content = new TextBlock { Text = "Not a recovery phrase" } };
		panel.Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/"))
		{
			Source = new Uri("avares://WalletWasabi.Fluent/Mobile/Styles/MobileBackupControls.axaml")
		});
		var parent = new Border { Padding = new Thickness(16), Child = panel };
		var window = CreateWindow(parent);
		try
		{
			window.Show(); Pump();
			var presenter = panel.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "PART_ContentPresenter");
			var reveal = panel.GetVisualDescendants().OfType<ToggleButton>().Single(x => x.Name == "PART_Reveal");
			var acknowledge = panel.GetVisualDescendants().OfType<CheckBox>().Single(x => x.Name == "PART_Acknowledge");
			Assert.False(panel.IsRevealed);
			Assert.False(presenter.IsVisible);
			Press(window, reveal);
			Assert.True(panel.IsRevealed);
			Assert.True(presenter.IsVisible);
			Press(window, acknowledge);
			Assert.True(panel.IsAcknowledged);
			parent.IsVisible = false; Pump();
			Assert.False(panel.IsRevealed);
			Assert.False(panel.IsAcknowledged);
			parent.IsVisible = true; Pump();
			Assert.False(panel.IsRevealed);
			panel.IsRevealed = true; panel.IsAcknowledged = true;
			panel.DataContext = new object();
			Assert.False(panel.IsRevealed);
			Assert.False(panel.IsAcknowledged);
			panel.IsRevealed = true; panel.IsAcknowledged = true;
			parent.Child = null; Pump();
			Assert.False(panel.IsRevealed);
			Assert.False(panel.IsAcknowledged);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void VerificationSlotsDisplayOnlyTheUserSelectionNotTheExpectedWord()
	{
		var word = new RecoveryWordViewModel(null!, 1, "synthetic-expected");
		word.Reset();
		var view = new MobileConfirmRecoveryWordsView();
		var slots = view.FindControl<ItemsControl>("VerificationSlots")!;
		slots.ItemsSource = new[] { word };
		var window = CreateWindow(view);
		try
		{
			window.Show(); Pump();
			var tile = Assert.Single(slots.GetVisualDescendants().OfType<MobileRecoveryWordTile>());
			Assert.Null(tile.Word);
			Assert.False(tile.IsConfirmed);
			word.SelectedWord = "synthetic-wrong"; Pump();
			Assert.Equal("synthetic-wrong", tile.Word);
			Assert.False(tile.IsConfirmed);
			word.SelectedWord = "synthetic-expected"; Pump();
			Assert.Equal(word.SelectedWord, tile.Word);
			Assert.True(tile.IsConfirmed);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void ConfirmedCandidatesCollapseTheirContainersAndRetainTwoWaySelection()
	{
		var words = Enumerable.Range(1, 6).Select(index => new RecoveryWordViewModel(null!, index, $"test-{index}")).ToArray();
		foreach (var word in words) word.Reset();
		var view = new MobileConfirmRecoveryWordsView();
		var candidates = view.FindControl<ItemsControl>("CandidateWords")!;
		candidates.ItemsSource = words;
		candidates.IsVisible = true;
		var window = CreateWindow(view);
		try
		{
			window.Show(); Pump();
			var grid = Assert.Single(candidates.GetVisualDescendants().OfType<UniformGrid>());
			Assert.Equal(6, grid.Children.Count(x => x.IsVisible));
			var button = candidates.GetVisualDescendants().OfType<ToggleButton>().Single(x => Equals(x.Content, "test-6"));
			Press(window, button);
			Assert.True(words[5].IsSelected);
			for (var index = 0; index < 5; index++) words[index].IsConfirmed = true;
			Pump();
			Assert.Single(grid.Children, x => x.IsVisible);
			Assert.True(button.IsVisible);
			Assert.InRange(grid.Bounds.Height, 1, 100);
		}
		finally { window.Close(); }
	}

	private static void DisposeCommands(WalletWasabi.Fluent.ViewModels.Navigation.RoutableViewModel model)
	{
		(model.NextCommand as IDisposable)?.Dispose();
		(model.SkipCommand as IDisposable)?.Dispose();
		(model.BackCommand as IDisposable)?.Dispose();
		(model.CancelCommand as IDisposable)?.Dispose();
	}

	private static Window CreateWindow(Control content, bool dark = false) => new()
	{
		Width = 390, Height = 844, SystemDecorations = SystemDecorations.None,
		Content = content, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
	};
	private static void Pump() => Dispatcher.UIThread.RunJobs();
	private static void Press(Window window, InputElement control)
	{
		Assert.True(control.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
		Pump();
	}
}
