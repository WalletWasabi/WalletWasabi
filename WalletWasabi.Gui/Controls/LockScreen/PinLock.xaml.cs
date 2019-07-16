using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using Avalonia;
using Avalonia.Interactivity;
using System.Reactive.Linq;
using System.Security.Cryptography;
using WalletWasabi.Helpers;
using System.Reactive;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class PinLock : LockScreenBase
	{
		public Grid Shade { get; }

		public ReactiveCommand<string, Unit> KeyPadCommand { get; }

		private NoparaPasswordBox PinLockPwdBox;

		internal string PINHash { get; set; }

		public PinLock() : base()
		{
			InitializeComponent();

			this.Shade = this.FindControl<Grid>("Shade");
			this.PinLockPwdBox = this.FindControl<NoparaPasswordBox>("PinLockPwdBox");

			PinLockPwdBox.WhenAnyValue(x => x.Password)
						 .Select(Guard.Correct)
						 .Where(x => x != string.Empty)
						 .DistinctUntilChanged()
						 .Throttle(TimeSpan.FromSeconds(1d))
						 .ObserveOn(RxApp.MainThreadScheduler)
						 .Subscribe(CheckPIN);

			KeyPadCommand = ReactiveCommand.Create<string>((arg) =>
			{
				PinLockPwdBox.Password += arg;
			});
		}

		private void CheckPIN(string obj)
		{
			var currentHash = HashHelpers.GenerateSha256Hash(obj);
			if (currentHash == PINHash)
			{
				PinLockPwdBox.Password = "";
				this.IsLocked = false;
			}
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void DoLock()
		{
			Shade.Classes.Add("Locked");
			Shade.Classes.Remove("Unlocked");
		}

		public override void DoUnlock()
		{
			Shade.Classes.Add("Unlocked");
			Shade.Classes.Remove("Locked");
		}
	}
}
