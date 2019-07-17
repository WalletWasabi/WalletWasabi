using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    public interface ILockScreenViewModel : IDisposable
    {
    }
}
