using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Helpers;

public static class MobileAutomation
{
	private enum AutomationState
	{
		CreateWallet,
		GenerateAddress,
		NavigateToSend,
		FillSendDetails,
		PrivacyControl,
		SendFee,
		ConfirmTransaction,
		Completed
	}

	private static AutomationState _state = AutomationState.CreateWallet;
	private static string _generatedAddress = "";

	private static string? GetLogPath()
	{
		return Environment.GetEnvironmentVariable("WASABI_AUTOMATE_LOG_PATH");
	}

	private static void Log(string message)
	{
		Console.WriteLine(message);
		var path = GetLogPath();
		if (string.IsNullOrEmpty(path))
		{
			return;
		}
		try
		{
			File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}\n");
		}
		catch
		{
			// ignore
		}
	}

	public static void Start(MainViewModel mainViewModel)
	{
		if (Environment.GetEnvironmentVariable("WASABI_AUTOMATE_MOBILE") != "1")
		{
			return;
		}

		// Clear log file at startup
		var path = GetLogPath();
		if (!string.IsNullOrEmpty(path))
		{
			try { File.Delete(path); } catch {}
		}

		Log("[MobileAutomation] Starting generic mobile automation service...");

		var navState = mainViewModel.UiContext.Navigate() as NavigationState;
		if (navState == null)
		{
			Log("[MobileAutomation] Error: NavigationState is null");
			return;
		}

		// Subscribe to active page changes
		navState.WhenAnyValue(
				x => x.DialogScreen.CurrentPage,
				x => x.CompactDialogScreen.CurrentPage,
				x => x.FullScreen.CurrentPage,
				x => x.HomeScreen.CurrentPage,
				(dialog, compactDialog, fullScreenDialog, mainScreen) => compactDialog ?? dialog ?? fullScreenDialog ?? mainScreen)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(OnPageChanged);
	}

	private static void OnPageChanged(RoutableViewModel page)
	{
		var pageName = page.GetType().Name;
		Log($"[MobileAutomation] Active page changed to: {pageName}");

		Task.Run(async () =>
		{
			await Task.Delay(2500); // Wait for transition animation and layout

			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				try
				{
					// Resolve the visual root window or view
					Visual? visualRoot = null;
					if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
					{
						visualRoot = desktop.MainWindow;
					}
					else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single)
					{
						visualRoot = single.MainView;
					}

					if (visualRoot == null)
					{
						Log("[MobileAutomation] Error: Visual root not found.");
						return;
					}

					Log($"[MobileAutomation] Entered {pageName}");

					// Passive state tracking for log/screenshot synchronization
					if (pageName == "WalletViewModel")
					{
						if (_state == AutomationState.CreateWallet)
						{
							Log("[MobileAutomation] Entered WalletPage - Init");
							_state = AutomationState.GenerateAddress;
						}
						else if (_state == AutomationState.NavigateToSend)
						{
							Log("[MobileAutomation] Entered WalletPage - Return");
							_state = AutomationState.FillSendDetails;
						}
					}
					else if (pageName == "ReceiveAddressViewModel")
					{
						_state = AutomationState.NavigateToSend;
					}

					if (System.Environment.GetEnvironmentVariable("WASABI_AUTOMATE_ACTIVE") != "1")
					{
						return;
					}

					switch (pageName)
					{
						case "WelcomePageViewModel":
							Log("[MobileAutomation] Automating WelcomePage...");
							var welcomeBtn = FindControl(visualRoot, c => GetControlText(c) == "Get Started" || c.Name == "PART_NextButton");
							if (welcomeBtn != null)
							{
								Log($"[MobileAutomation] Found WelcomePage button: {welcomeBtn.Name ?? welcomeBtn.GetType().Name}");
								InvokeControl(welcomeBtn);
							}
							break;

						case "AddWalletPageViewModel":
							Log("[MobileAutomation] Automating AddWalletPage...");
							var createBtn = FindControl(visualRoot, c => c.Name == "CreateButton" || GetControlText(c) == "Create a new wallet");
							if (createBtn != null)
							{
								Log($"[MobileAutomation] Found AddWalletPage button: {createBtn.Name ?? createBtn.GetType().Name}");
								InvokeControl(createBtn);
							}
							break;

						case "WalletBackupTypeViewModel":
							Log("[MobileAutomation] Automating WalletBackupType page...");
							var singleMnemonicItem = FindControl(visualRoot, c => GetControlText(c)?.Contains("Single mnemonic phrase") == true);
							if (singleMnemonicItem != null)
							{
								Log($"[MobileAutomation] Selecting backup option...");
								SelectControl(singleMnemonicItem);
							}
							var backupNextBtn = FindControl(visualRoot, c => c.Name == "PART_NextButton" || GetControlText(c) == "Continue");
							if (backupNextBtn != null)
							{
								Log($"[MobileAutomation] Invoking Backup Next button");
								InvokeControl(backupNextBtn);
							}
							break;

						case "RecoveryWordsViewModel":
							Log("[MobileAutomation] Automating RecoveryWords page...");
							var recNextBtn = FindControl(visualRoot, c => c.Name == "PART_NextButton" || GetControlText(c) == "Continue");
							if (recNextBtn != null)
							{
								Log($"[MobileAutomation] Invoking RecoveryWords Next button");
								InvokeControl(recNextBtn);
							}
							break;

						case "ConfirmRecoveryWordsViewModel":
							Log("[MobileAutomation] Automating ConfirmRecoveryWords page...");
							var skipBtn = FindControl(visualRoot, c => c.Name == "PART_SkipButton" || c.Name == "SkipButton" || GetControlText(c) == "Skip");
							if (skipBtn != null)
							{
								Log($"[MobileAutomation] Invoking ConfirmRecoveryWords Skip button");
								InvokeControl(skipBtn);
							}
							break;

						case "CreatePasswordDialogViewModel":
							Log("[MobileAutomation] Automating CreatePasswordDialog page...");
							var pwdProp = page.GetType().GetProperty("Password");
							var confPwdProp = page.GetType().GetProperty("ConfirmPassword");
							pwdProp?.SetValue(page, "");
							confPwdProp?.SetValue(page, "");
							var pwdNextCmd = page.GetType().GetProperty("NextCommand")?.GetValue(page) as System.Windows.Input.ICommand;
							if (pwdNextCmd != null && pwdNextCmd.CanExecute(null))
							{
								Log($"[MobileAutomation] Executing CreatePasswordDialog NextCommand");
								pwdNextCmd.Execute(null);
							}
							else
							{
								Log("[MobileAutomation] CreatePasswordDialog NextCommand cannot execute.");
							}
							break;

						case "AddedWalletPageViewModel":
							Log("[MobileAutomation] Automating AddedWalletPage success page...");
							var doneBtn = FindControl(visualRoot, c => c.Name == "PART_NextButton" || GetControlText(c) == "Done");
							if (doneBtn != null)
							{
								Log($"[MobileAutomation] Invoking AddedWalletPage Done button");
								InvokeControl(doneBtn);
							}
							break;

						case "WalletViewModel":
							if (_state == AutomationState.CreateWallet)
							{
								_state = AutomationState.GenerateAddress;
								Log("[MobileAutomation] Entered WalletPage - Init");
								Log("[MobileAutomation] Wallet created! Triggering Receive flow...");
								var recCmdProp = page.GetType().GetProperty("SegwitReceiveCommand");
								var recCmd = recCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (recCmd != null && recCmd.CanExecute(null))
								{
									recCmd.Execute(null);
								}
								else
								{
									Log("[MobileAutomation] Error: SegwitReceiveCommand cannot execute.");
								}
							}
							else if (_state == AutomationState.NavigateToSend)
							{
								_state = AutomationState.FillSendDetails;
								Log("[MobileAutomation] Entered WalletPage - Return");
								Log("[MobileAutomation] Returned to dashboard. Triggering Send flow...");
								var sendCmdProp = page.GetType().GetProperty("SendCommand");
								var sendCmd = sendCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (sendCmd != null && sendCmd.CanExecute(null))
								{
									sendCmd.Execute(null);
								}
								else
								{
									Log("[MobileAutomation] Error: SendCommand cannot execute.");
								}
							}
							break;

						case "ReceiveViewModel":
							if (_state == AutomationState.GenerateAddress)
							{
								Log("[MobileAutomation] Automating ReceivePage label input...");
								var sugLabelsProp = page.GetType().GetProperty("SuggestionLabels");
								if (sugLabelsProp != null)
								{
									var sugLabels = sugLabelsProp.GetValue(page);
									if (sugLabels != null)
									{
										var labelsProp = sugLabels.GetType().GetProperty("Labels");
										var labels = labelsProp?.GetValue(sugLabels) as System.Collections.ObjectModel.ObservableCollection<string>;
										if (labels != null && !labels.Contains("AutoLabel"))
										{
											labels.Add("AutoLabel");
										}
									}
								}
								var nextCmdProp = page.GetType().GetProperty("NextCommand");
								var nextCmd = nextCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (nextCmd != null && nextCmd.CanExecute(null))
								{
									nextCmd.Execute(null);
								}
								else
								{
									Log("[MobileAutomation] Error: Receive next command cannot execute.");
								}
							}
							break;

						case "ReceiveAddressViewModel":
							if (_state == AutomationState.GenerateAddress)
							{
								var addressProp = page.GetType().GetProperty("Address");
								_generatedAddress = addressProp?.GetValue(page)?.ToString() ?? "";
								Log($"[MobileAutomation] Saved generated address: {_generatedAddress}");

								_state = AutomationState.NavigateToSend;
								var nextCmdProp = page.GetType().GetProperty("NextCommand");
								var nextCmd = nextCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (nextCmd != null && nextCmd.CanExecute(null))
								{
									nextCmd.Execute(null);
								}
							}
							break;

						case "SendViewModel":
							if (_state == AutomationState.FillSendDetails)
							{
								Log("[MobileAutomation] Filling Send details...");
								var toProp = page.GetType().GetProperty("To");
								toProp?.SetValue(page, _generatedAddress);

								var amountProp = page.GetType().GetProperty("AmountBtc");
								amountProp?.SetValue(page, 0.005m);

								var sugLabelsProp = page.GetType().GetProperty("SuggestionLabels");
								if (sugLabelsProp != null)
								{
									var sugLabels = sugLabelsProp.GetValue(page);
									if (sugLabels != null)
									{
										var labelsProp = sugLabels.GetType().GetProperty("Labels");
										var labels = labelsProp?.GetValue(sugLabels) as System.Collections.ObjectModel.ObservableCollection<string>;
										if (labels != null && !labels.Contains("AutoLabel"))
										{
											labels.Add("AutoLabel");
										}
									}
								}

								var nextCmdProp = page.GetType().GetProperty("NextCommand");
								var nextCmd = nextCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (nextCmd != null && nextCmd.CanExecute(null))
								{
									nextCmd.Execute(null);
								}
								else
								{
									Log("[MobileAutomation] Error: Send next command cannot execute.");
								}
							}
							break;

						case "PrivacyControlViewModel":
							if (_state == AutomationState.FillSendDetails || _state == AutomationState.PrivacyControl)
							{
								_state = AutomationState.PrivacyControl;
								Log("[MobileAutomation] Automating PrivacyControl page...");
								var nextCmdProp = page.GetType().GetProperty("NextCommand");
								var nextCmd = nextCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (nextCmd != null && nextCmd.CanExecute(null))
								{
									nextCmd.Execute(null);
								}
							}
							break;

						case "SendFeeViewModel":
							if (_state == AutomationState.PrivacyControl || _state == AutomationState.SendFee)
							{
								_state = AutomationState.SendFee;
								Log("[MobileAutomation] Automating SendFee page...");
								var nextCmdProp = page.GetType().GetProperty("NextCommand");
								var nextCmd = nextCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (nextCmd != null && nextCmd.CanExecute(null))
								{
									nextCmd.Execute(null);
								}
							}
							break;

						case "TransactionPreviewViewModel":
							if (_state == AutomationState.SendFee || _state == AutomationState.ConfirmTransaction)
							{
								_state = AutomationState.ConfirmTransaction;
								Log("[MobileAutomation] Automating TransactionPreview page...");
								var nextCmdProp = page.GetType().GetProperty("NextCommand");
								var nextCmd = nextCmdProp?.GetValue(page) as System.Windows.Input.ICommand;
								if (nextCmd != null && nextCmd.CanExecute(null))
								{
									nextCmd.Execute(null);
								}
							}
							break;

						case "SendSuccessViewModel":
							Log("[MobileAutomation] Automating SendSuccess page...");
							_state = AutomationState.Completed;
							var nextCmdProp2 = page.GetType().GetProperty("NextCommand");
							var nextCmd2 = nextCmdProp2?.GetValue(page) as System.Windows.Input.ICommand;
							if (nextCmd2 != null && nextCmd2.CanExecute(null))
							{
								nextCmd2.Execute(null);
							}
							break;
					}
				}
				catch (Exception ex)
				{
					Log($"[MobileAutomation] Error automating page {pageName}: {ex}");
				}
			});
		});
	}

	private static Control? FindControl(Visual root, Func<Control, bool> predicate)
	{
		if (root is Control ctrl && predicate(ctrl))
		{
			return ctrl;
		}

		foreach (var child in root.GetVisualChildren())
		{
			var found = FindControl(child, predicate);
			if (found != null)
			{
				return found;
			}
		}
		return null;
	}

	private static string? GetControlText(Control control)
	{
		if (control is ContentControl cc && cc.Content is string s)
		{
			return s;
		}
		if (control is TextBlock tb)
		{
			return tb.Text;
		}
		
		var textProp = control.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
		if (textProp != null && textProp.GetValue(control) is string text)
		{
			return text;
		}
		
		var autoName = AutomationProperties.GetName(control);
		if (!string.IsNullOrEmpty(autoName))
		{
			return autoName;
		}

		return null;
	}

	private static bool InvokeControl(Control control)
	{
		var peer = ControlAutomationPeer.CreatePeerForElement(control);
		var invoke = peer as IInvokeProvider;
		if (invoke != null)
		{
			invoke.Invoke();
			return true;
		}

		if (control is Button btn)
		{
			if (btn.Command != null && btn.Command.CanExecute(btn.CommandParameter))
			{
				btn.Command.Execute(btn.CommandParameter);
				return true;
			}
		}
		
		var cmdProp = control.GetType().GetProperty("Command", BindingFlags.Public | BindingFlags.Instance);
		if (cmdProp != null && cmdProp.GetValue(control) is System.Windows.Input.ICommand cmd)
		{
			if (cmd.CanExecute(null))
			{
				cmd.Execute(null);
				return true;
			}
		}

		return false;
	}

	private static bool SelectControl(Control control)
	{
		if (control is ListBoxItem lbi)
		{
			lbi.IsSelected = true;
			return true;
		}

		if (control is RadioButton rb)
		{
			rb.IsChecked = true;
			return true;
		}

		var parent = control.GetVisualParent();
		while (parent != null)
		{
			if (parent is ListBoxItem parentLbi)
			{
				parentLbi.IsSelected = true;
				return true;
			}
			if (parent is RadioButton parentRb)
			{
				parentRb.IsChecked = true;
				return true;
			}
			if (parent is Control parentCtrl)
			{
				var peer = ControlAutomationPeer.CreatePeerForElement(parentCtrl);
				var selectionItem = peer as ISelectionItemProvider;
				if (selectionItem != null)
				{
					selectionItem.Select();
					return true;
				}
			}
			parent = parent.GetVisualParent();
		}

		return false;
	}
}
