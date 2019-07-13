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

namespace WalletWasabi.Gui.Controls.LockScreen
{
    public class PINLock : LockScreenBase
    {
        public Grid Shade { get; }

        private NoparaPasswordBox PinLockPwdBox;

        internal string PINHash { get; set; }

        public PINLock() : base()
        {
            InitializeComponent();
			
            this.Shade = this.FindControl<Grid>("Shade");
            this.PinLockPwdBox = this.FindControl<NoparaPasswordBox>("PinLockPwdBox");

            PinLockPwdBox.WhenAnyValue(x => x.Password)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Select(Guard.Correct)
                         .Where(x => x != string.Empty)
                         .DistinctUntilChanged()
                         .Throttle(TimeSpan.FromSeconds(1d))
                         .Subscribe(CheckPIN);
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
