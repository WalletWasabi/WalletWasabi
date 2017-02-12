using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DevZH.UI.Events;
using DevZH.UI.Interface;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class Application : IDisposable
    {
        private static Application _instance;
	    internal static Window MainWindow { get; private set; }
        private static object _lock = new object();
        private static bool _appCreated;

        public static Application Current => _instance;
        public event EventHandler<CancelEventArgs> OnShouldExit;

        protected InitOptions Options = new InitOptions {Size = UIntPtr.Zero};

        private int _exitCode;

        public int ExitCode
        {
            set
            {
                if (value != _exitCode)
                {
                    _exitCode = value;
                }
            }
        }

        public Application(bool hiddenConsole = true)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var locks = _lock;
            lock (locks)
            {
                if (_appCreated)
                {
                    throw new InvalidOperationException("Cannot create more than one Application everytime.");
                }
                _instance = this;
                _appCreated = true;
                Init(hiddenConsole);
            }
        }

        private bool _hideConsole;

        public bool HideConsole
        {
            set
            {
                // ConsoleApplication will show a Console Window on Windows, I have to hide it by default.
                // It still appear in seconds.
                // The side-effect is that it'll also close the `dotnet run` Console
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (_hideConsole == value) return;
                    var handle = NativeMethods.GetConsoleWindow();
                    if (value)
                    {
                        var SW_HIDE = 0;
                        NativeMethods.ShowWindow(handle, SW_HIDE);
                    }
                    else
                    {
                        var SW_SHOWNOACTIVATE = 4;
                        NativeMethods.ShowWindow(handle, SW_SHOWNOACTIVATE);
                    }
                    _hideConsole = value;
                }
            }
        }

        private void Init(bool hiddenConsole = true)
        {
            HideConsole = hiddenConsole;
            var str = NativeMethods.Init(ref Options);
            if (!string.IsNullOrEmpty(str))
            {
                Console.WriteLine(str);
                NativeMethods.FreeInitError(str);
                throw new Win32Exception(str);
            }
            InitializeEvents();
        }

        public int Run(Window window)
        {
            MainWindow = window;
            return Run(window.Show);
            //window.Show();
            //NativeMethods.Main();
            //return 0;
        }

        protected int Run(Action action)
        {
            try
            {
                QueueMain(action);
                NativeMethods.Main();
            }
#pragma warning disable CS0168 // ReSharper disable once UnusedVariable
			catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
            {
                return -1;
            }
            return 0;
        }

        public void QueueMain(Action action) => NativeMethods.QueueMain(data => action?.Invoke(), IntPtr.Zero);

	    private void Steps()
        {
            NativeMethods.MainSteps();
        }

        private bool Step(bool wait)
        {
            return NativeMethods.MainStep(wait);
        }

        public void Dispose()
        {
	        MainWindow?.Dispose();
	        NativeMethods.UnInit();
        }

        public void Exit()
        {
            NativeMethods.Quit();
        }

        private void InitializeEvents()
        {
            NativeMethods.OnShouldQuit(data =>
            {
                var args = new CancelEventArgs();
                OnShouldExit?.Invoke(this, args);
                return !args.Cancel;
            }, IntPtr.Zero);
            
        }
    }
}
