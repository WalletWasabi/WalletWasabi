using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Logging;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Settings;

public partial class GeneralSettingsTabViewModel : SettingsTabViewModelBase
{
	[AutoNotify] private bool _darkModeEnabled;
	[AutoNotify] private bool _autoCopy;
	[AutoNotify] private bool _autoPaste;
	[AutoNotify] private bool _customChangeAddress;
	[AutoNotify] private FeeDisplayFormat _selectedFeeDisplayFormat;
	[AutoNotify] private bool _runOnSystemStartup;
	[AutoNotify] private bool _hideOnClose;

	public GeneralSettingsTabViewModel()
	{
		_darkModeEnabled = Services.UiConfig.DarkModeEnabled;
		_autoCopy = Services.UiConfig.Autocopy;
		_autoPaste = Services.UiConfig.AutoPaste;
		_customChangeAddress = Services.UiConfig.IsCustomChangeAddress;
		_runOnSystemStartup = Services.UiConfig.RunOnSystemStartup;
		_hideOnClose = Services.UiConfig.HideOnClose;
		_selectedFeeDisplayFormat = Enum.IsDefined(typeof(FeeDisplayFormat), Services.UiConfig.FeeDisplayFormat)
			? (FeeDisplayFormat)Services.UiConfig.FeeDisplayFormat
			: FeeDisplayFormat.Satoshis;

		ChangeTheme =
			ReactiveCommand.CreateFromTask(async () =>
			{
				var light = DarkModeEnabled ? Theme.Dark : Theme.Light;
				Services.UiConfig.DarkModeEnabled = DarkModeEnabled;
				await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(new ThemeChangeViewModel(light));
			});

		this.WhenAnyValue(x => x.DarkModeEnabled)
			.Skip(1)
			.Select(b => Unit.Default)
			.InvokeCommand(ChangeTheme);

		this.WhenAnyValue(x => x.AutoCopy)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.Autocopy = x);

		this.WhenAnyValue(x => x.AutoPaste)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.AutoPaste = x);

		RunOnSystemStartupCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				await StartupHelper.ModifyStartupSettingAsync(RunOnSystemStartup);
				Services.UiConfig.RunOnSystemStartup = RunOnSystemStartup;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				RunOnSystemStartup = !RunOnSystemStartup;
				await ShowError("Error occurred", "Couldn't save your change, please see the logs for further information.", "");
			}
		});

		this.WhenAnyValue(x => x.CustomChangeAddress)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.IsCustomChangeAddress = x);

		this.WhenAnyValue(x => x.SelectedFeeDisplayFormat)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.FeeDisplayFormat = (int)x);

		this.WhenAnyValue(x => x.HideOnClose)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.HideOnClose = x);
	}

	private static Task<DialogResult<bool>> ShowError(string caption, string message, string title)
	{
		var error = new ShowErrorDialogViewModel(message, title, caption);
		return MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(error);
	}

	public ICommand ChangeTheme { get; }

	public ICommand RunOnSystemStartupCommand { get; }

	public IEnumerable<FeeDisplayFormat> FeeDisplayFormats =>
		Enum.GetValues(typeof(FeeDisplayFormat)).Cast<FeeDisplayFormat>();

	protected override void EditConfigOnSave(Config config)
	{
	}
}
