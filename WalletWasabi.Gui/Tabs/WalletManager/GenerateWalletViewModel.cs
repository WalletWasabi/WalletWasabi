﻿using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _walletName;
		private bool _termsAccepted;
		private string _validationMessage;
		public WalletManagerViewModel Owner { get; }

		public GenerateWalletViewModel(WalletManagerViewModel owner) : base("Generate Wallet")
		{
			Owner = owner;

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				DoGenerateCommand();
			},
			this.WhenAnyValue(x => x.TermsAccepted));

			this.WhenAnyValue(x => x.Password).Subscribe(x =>
			{
				try
				{
					if (x.NotNullAndNotEmpty())
					{
						char lastChar = x.Last();
						if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
						{
							Password = x.TrimEnd('\r', '\n');
							if (TermsAccepted)
							{
								DoGenerateCommand();
							}
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogTrace(ex);
				}
			});
		}

		private void DoGenerateCommand()
		{
			WalletName = Guard.Correct(WalletName);

			string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");
			Password = Guard.Correct(Password); // Don't let whitespaces to the beginning and to the end.

			if (!TermsAccepted)
			{
				ValidationMessage = "Terms are not accepted.";
			}
			else if (string.IsNullOrWhiteSpace(WalletName))
			{
				ValidationMessage = $"The name {WalletName} is not valid.";
			}
			else if (File.Exists(walletFilePath))
			{
				ValidationMessage = $"The name {WalletName} is already taken.";
			}
			else
			{
				try
				{
					KeyManager.CreateNew(out Mnemonic mnemonic, Password, walletFilePath);

					Owner.CurrentView = new GenerateWalletSuccessViewModel(Owner, mnemonic);
				}
				catch (Exception ex)
				{
					ValidationMessage = ex.ToTypeMessageString();
					Logger.LogError<GenerateWalletViewModel>(ex);
				}
			}
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string WalletName
		{
			get => _walletName;
			set => this.RaiseAndSetIfChanged(ref _walletName, value);
		}

		public bool TermsAccepted
		{
			get => _termsAccepted;
			set => this.RaiseAndSetIfChanged(ref _termsAccepted, value);
		}

		public string ValidationMessage
		{
			get => _validationMessage;
			set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
		}

		public ReactiveCommand GenerateCommand { get; }

		public void OnTermsClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new TermsAndConditionsViewModel());
		}

		public void OnPrivacyClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new PrivacyPolicyViewModel());
		}

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new LegalIssuesViewModel());
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = "";
			WalletName = Utils.GetNextWalletName();
			TermsAccepted = false;
			ValidationMessage = "";
		}
	}
}
