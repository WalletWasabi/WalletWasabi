using System.IO;
using System.Runtime.InteropServices;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// Talks to one Trezor over USB through libusb, without a bridge in between.
/// Not thread safe: the device answers one request at a time, so the caller sends one at a time,
/// and cancels a pending read and waits for it before disposing.
/// </summary>
public class TrezorUsbTransport : IDisposable
{
	// Model T and the Safe family in normal mode. The Model One speaks HID and the bootloader has another id.
	private const ushort VendorId = 0x1209;
	private const ushort ProductId = 0x53C1;

	private const int WireInterface = 0;
	private const byte EndpointOut = 0x01;
	private const byte EndpointIn = 0x81;

	private const uint WriteTimeoutMs = 5000;

	/// <summary>A read waits for the user at the device, so it has no deadline. This is how often it looks at the cancellation token.</summary>
	private const uint ReadPollMs = 1000;

	private TrezorUsbTransport(IntPtr context, IntPtr handle)
	{
		_context = context;
		_handle = handle;
	}

	private readonly IntPtr _context;
	private IntPtr _handle;

	/// <summary>Paths of the connected devices, as "bus:address". A path changes when the device is plugged in again.</summary>
	public static IReadOnlyList<string> Enumerate()
	{
		IntPtr context = Init();
		try
		{
			var paths = new List<string>();
			ForEachTrezor(context, (path, _) =>
			{
				paths.Add(path);
				return true;
			});
			return paths;
		}
		finally
		{
			LibUsb.Exit(context);
		}
	}

	/// <exception cref="IOException">The device is gone, or it cannot be opened: no permission, or another program holds it.</exception>
	public static TrezorUsbTransport Open(string path)
	{
		IntPtr context = Init();
		IntPtr handle = IntPtr.Zero;
		try
		{
			ForEachTrezor(context, (candidate, device) =>
			{
				if (candidate != path)
				{
					return true;
				}
				Check(LibUsb.Open(device, out handle), "open the device");
				return false;
			});

			if (handle == IntPtr.Zero)
			{
				throw new IOException($"No Trezor at USB path '{path}'.");
			}

			// Linux only: the kernel may hold the interface. Other systems answer "not supported".
			_ = LibUsb.SetAutoDetachKernelDriver(handle, 1);
			Check(LibUsb.ClaimInterface(handle, WireInterface), "claim the device");
			return new TrezorUsbTransport(context, handle);
		}
		catch
		{
			if (handle != IntPtr.Zero)
			{
				LibUsb.Close(handle);
			}
			LibUsb.Exit(context);
			throw;
		}
	}

	public void Write(TrezorMessage message)
	{
		ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);

		foreach (byte[] report in TrezorWireCodec.Encode(message))
		{
			Check(LibUsb.InterruptTransfer(_handle, EndpointOut, report, report.Length, out int transferred, WriteTimeoutMs), "write to the device");
			if (transferred != report.Length)
			{
				throw new IOException($"The device took {transferred} of {report.Length} bytes.");
			}
		}
	}

	/// <summary>
	/// Reads the next message. Blocks the calling thread until the device answers, which can be as long as the user takes.
	/// A canceled read leaves the answer in the device: the next message read is then that late answer.
	/// </summary>
	public TrezorMessage Read(CancellationToken cancellationToken) =>
		TrezorWireCodec.Decode(() => ReadReport(cancellationToken));

	private byte[] ReadReport(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);

		var report = new byte[TrezorWireCodec.ReportLength];
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// A report that arrives just as the poll times out comes back with the timeout error, so the length decides.
			int result = LibUsb.InterruptTransfer(_handle, EndpointIn, report, report.Length, out int transferred, ReadPollMs);
			if (transferred > 0)
			{
				return report[..transferred];
			}
			if (result != LibUsb.ErrorTimeout)
			{
				Check(result, "read from the device");
			}
		}
	}

	public void Dispose()
	{
		if (_handle == IntPtr.Zero)
		{
			return;
		}

		_ = LibUsb.ReleaseInterface(_handle, WireInterface);
		LibUsb.Close(_handle);
		_handle = IntPtr.Zero;
		LibUsb.Exit(_context);
		GC.SuppressFinalize(this);
	}

	private static IntPtr Init()
	{
		try
		{
			Check(LibUsb.Init(out IntPtr context), "start libusb");
			return context;
		}
		catch (DllNotFoundException e)
		{
			throw new IOException("Could not load the bundled libusb-1.0, which is needed to talk to a Trezor.", e);
		}
	}

	/// <summary>Calls <paramref name="visit"/> for every connected Trezor until it returns false. The device pointer is valid only inside the call.</summary>
	private static void ForEachTrezor(IntPtr context, Func<string, IntPtr, bool> visit)
	{
		nint count = LibUsb.GetDeviceList(context, out IntPtr list);
		Check((int)Math.Min(count, 0), "list USB devices");
		try
		{
			for (int i = 0; i < count; i++)
			{
				IntPtr device = Marshal.ReadIntPtr(list, i * IntPtr.Size);
				if (LibUsb.GetDeviceDescriptor(device, out var descriptor) == 0
					&& descriptor is { VendorId: VendorId, ProductId: ProductId }
					&& !visit($"{LibUsb.GetBusNumber(device)}:{LibUsb.GetDeviceAddress(device)}", device))
				{
					return;
				}
			}
		}
		finally
		{
			LibUsb.FreeDeviceList(list, unrefDevices: 1);
		}
	}

	private static void Check(int result, string action)
	{
		if (result < 0)
		{
			string hint = result switch
			{
				LibUsb.ErrorAccess when OperatingSystem.IsLinux() => " Install the udev rules for Trezor, or close the program that uses it.",
				LibUsb.ErrorAccess or LibUsb.ErrorBusy => " Close the program that uses it, like Trezor Suite.",
				_ => ""
			};
			throw new IOException($"Could not {action}: {LibUsb.ErrorName(result)}.{hint}");
		}
	}
}
