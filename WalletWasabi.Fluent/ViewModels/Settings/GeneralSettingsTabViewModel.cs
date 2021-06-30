using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using Microsoft.Win32;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	[NavigationMetaData(
		Title = "General",
		Caption = "Manage general settings",
		Order = 0,
		Category = "Settings",
		Keywords = new[]
		{
			"Settings", "General", "Dark", "Mode", "Bitcoin", "Addresses", "Manual", "Entry", "Fee", "Custom", "Change",
			"Address", "Display", "Format", "Dust", "Threshold", "BTC"
		},
		IconName = "settings_general_regular")]
	public partial class GeneralSettingsTabViewModel : SettingsTabViewModelBase
	{
		[AutoNotify] private bool _darkModeEnabled;
		[AutoNotify] private bool _autoCopy;
		[AutoNotify] private bool _customFee;
		[AutoNotify] private bool _customChangeAddress;
		[AutoNotify] private FeeDisplayFormat _selectedFeeDisplayFormat;
		[AutoNotify] private bool _osStartup;

		public GeneralSettingsTabViewModel()
		{
			_darkModeEnabled = Services.UiConfig.DarkModeEnabled;
			_autoCopy = Services.UiConfig.Autocopy;
			_customFee = Services.UiConfig.IsCustomFee;
			_customChangeAddress = Services.UiConfig.IsCustomChangeAddress;
			_selectedFeeDisplayFormat = Enum.IsDefined(typeof(FeeDisplayFormat), Services.UiConfig.FeeDisplayFormat)
				? (FeeDisplayFormat)Services.UiConfig.FeeDisplayFormat
				: FeeDisplayFormat.SatoshiPerByte;

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

			this.WhenAnyValue(x => x.OsStartup)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Skip(1)
				.Subscribe(x => ModifyRegistry(x));

			this.WhenAnyValue(x => x.CustomFee)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Skip(1)
				.Subscribe(x => Services.UiConfig.IsCustomFee = x);

			this.WhenAnyValue(x => x.CustomChangeAddress)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Skip(1)
				.Subscribe(x => Services.UiConfig.IsCustomChangeAddress = x);

			this.WhenAnyValue(x => x.SelectedFeeDisplayFormat)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Skip(1)
				.Subscribe(x => Services.UiConfig.FeeDisplayFormat = (int)x);
		}

		public IEnumerable<FeeDisplayFormat> FeeDisplayFormats =>
			Enum.GetValues(typeof(FeeDisplayFormat)).Cast<FeeDisplayFormat>();

		// TODO: Save the changed option to the UI Config File
		private void ModifyRegistry(bool changedOption)
		{
			string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
			using RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName, true);
			if (changedOption)
			{
				string pathToExe = Assembly.GetExecutingAssembly().Location;
				pathToExe = pathToExe.Remove(pathToExe.Length - 11);        // This part has to change if this gets released
				pathToExe += ".Fluent.Desktop.exe";

				RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
				rkApp.SetValue("WasabiWallet", pathToExe);
			}
			else
			{
				key.DeleteValue("WasabiWallet");
			}
		}

		protected override void EditConfigOnSave(Config config)
		{
		}
	}
}
