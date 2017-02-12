using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Drawing;
using DevZH.UI.Interface;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class Window : Control
    {
        private string _title = "DevZH.UI";

        private static readonly Dictionary<IntPtr, Window> Windows = new Dictionary<IntPtr, Window>();

        public EventHandler<CancelEventArgs> Closing;

        public Control Child
        {
            set
            {
                if (value != null && value.Verify())
                {
                    NativeMethods.WindowSetChild(handle, value.handle);
                }
            }
        }

        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                var title = value;
                if (!ValidTitle(title)) title = _title;
                NativeMethods.WindowSetTitle(handle, StringUtil.GetBytes(title));
                _title = title;
            }
        }

        private Point _location = new Point();

        public Point Location
        {
            get
            {
                int x, y;
                NativeMethods.WindowPosition(handle, out x, out y);
                _location.X = x;
                _location.Y = y;
                return _location;
            }
            set
            {
                _location = value;
                NativeMethods.WindowSetPosition(handle, (int) value.X, (int) value.Y);
                StartPosition = WindowStartPosition.Manual;
            }
        }

        private Size _size = new Size();
        public Size Size
        {
            get
            {
                int w, h;
                NativeMethods.WindowContentSize(handle, out w, out h);
                _size.Width = w;
                _size.Height = h;
                return _size;
            }
            set
            {
                _size = value;
                NativeMethods.WindowSetContentSize(handle, (int) value.Width, (int) value.Height);
            }
        }

        public bool AllowMargins
        {
            get { return NativeMethods.WindowMargined(handle); }
            set { NativeMethods.WindowSetMargined(handle, value);}
        }

        public bool FullScreen
        {
            get { return NativeMethods.WindowFullscreen(handle); }
            set { NativeMethods.WindowSetFullscreen(handle, value);}
        }

        public bool Borderless
        {
            get { return NativeMethods.WindowBorderless(handle); }
            set { NativeMethods.WindowSetBorderless(handle, value); }
        }

        public Window(string title, int width = 500, int height = 200, bool hasMenubar = false)
        {
            if (!ValidTitle(title)) title = _title;
            this.handle = NativeMethods.NewWindow(StringUtil.GetBytes(title), width, height, hasMenubar);
            _title = title;
            Windows.Add(this.handle, this);
            this.InitializeEvents();
            this.InitializeData();
        }

        protected void InitializeEvents()
        {
            if (!this.Verify())
            {
                throw new TypeInitializationException(nameof(Window), new InvalidComObjectException());
            }
            NativeMethods.WindowOnClosing(this.handle, (window, data) =>
            {
                var args = new CancelEventArgs();
                OnClosing(args);
                // args.Cancel: True is not closing. False is to be closed and destroyed.
                // It maybe a little different to other wrapper.
                var cancel = args.Cancel;
                if (!cancel)
                {
                    if (Windows.Count > 1)
                    {
                        var intptr = this.handle;
                        Windows.Remove(intptr);
                    }
                    else
                    {
                        Application.Current.Exit();
                    }
                }
                return !cancel;
            }, IntPtr.Zero);

            NativeMethods.WindowOnPositionChanged(this.handle, (window, data) =>
            {
                OnLocationChanged(EventArgs.Empty);
            }, IntPtr.Zero);

            NativeMethods.WindowOnContentSizeChanged(this.handle, (window, data) =>
            {
                OnResize(EventArgs.Empty);
            }, IntPtr.Zero);
        }

        private void InitializeData()
        {
        }

        protected virtual void OnClosing(CancelEventArgs e)
        {
            Closing?.Invoke(this, e);
        }

        protected bool ValidTitle(string title) => !string.IsNullOrEmpty(title);

        public WindowStartPosition StartPosition { get; set; }

        public override void Show()
        {
            switch (StartPosition)
            {
                case WindowStartPosition.CenterScreen:
                    CenterToScreen();
                    break;
            }
            base.Show();
        }

        public void Close()
        {
            var intptr = this.handle;
            Destroy();
            Windows.Remove(intptr);
        }

		public void CenterToScreen()
        {
            NativeMethods.WindowCenter(handle);
        }
    }
}
