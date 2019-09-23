using Avalonia;
using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;
using Avalonia.Controls.Platform.Surfaces;

namespace WalletWasabi.Gui.Controls
{
	internal class WritableFramebuffer : ILockedFramebuffer, IFramebufferPlatformSurface, IDisposable
	{
		public WritableFramebuffer(PixelFormat fmt, PixelSize size)
		{
			Format = fmt;
			var bpp = fmt == PixelFormat.Rgb565 ? 2 : 4;
			Size = size;
			RowBytes = bpp * size.Width;
			Address = Marshal.AllocHGlobal(size.Height * RowBytes);
		}

		public IntPtr Address { get; }

		public Vector Dpi { get; } = new Vector(96, 96);

		public PixelFormat Format { get; }

		public PixelSize Size { get; }

		public int RowBytes { get; }

		private bool IsDisposed;

		public void Dispose()
		{ 
			if (!IsDisposed)
			{
				Marshal.FreeHGlobal(Address);
				IsDisposed = true;
			}
		}

		public ILockedFramebuffer Lock()
		{
			return this;
		}
	}
}