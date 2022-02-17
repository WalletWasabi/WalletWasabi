using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Logging;
using System.Windows.Input;
using DynamicData;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "General",
	Caption = "Manage general settings",
	Order = 0,
	Category = "Settings",
	Keywords = new[]
	{
			"Settings", "General", "Dark", "Mode", "Bitcoin", "Addresses", "Manual", "Entry", "Fee", "Custom", "Change",
			"Address", "Display", "Format", "Dust", "Threshold", "BTC", "Start", "System"
	},
	IconName = "settings_general_regular")]
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

		this.WhenAnyValue(x => x.DarkModeEnabled)
			.Skip(1)
			.Subscribe(
				x =>
				{
					Services.UiConfig.DarkModeEnabled = x;
					Navigate(NavigationTarget.CompactDialogScreen).To(new ThemeChangeViewModel(x ? Theme.Dark : Theme.Light));
				});

		this.WhenAnyValue(x => x.AutoCopy)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.Autocopy = x);

		this.WhenAnyValue(x => x.AutoPaste)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.AutoPaste = x);

		StartupCommand = ReactiveCommand.Create(async () =>
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
				await ShowErrorAsync(Title, "Couldn't save your change, please see the logs for further information.", "Error occurred.");
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

	public ICommand StartupCommand { get; }

	public IEnumerable<FeeDisplayFormat> FeeDisplayFormats =>
		Enum.GetValues(typeof(FeeDisplayFormat)).Cast<FeeDisplayFormat>();

	protected override void EditConfigOnSave(Config config)
	{
	}
}
