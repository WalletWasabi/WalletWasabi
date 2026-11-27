using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using WalletWasabi.BundledApps;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>The few libusb-1.0 functions a Trezor transport needs. Synchronous API only: no event thread, no callbacks.</summary>
internal static class LibUsb
{
	private const string Library = "libusb-1.0";

	public const int ErrorAccess = -3;
	public const int ErrorBusy = -6;
	public const int ErrorTimeout = -7;

	static LibUsb()
	{
		NativeLibrary.SetDllImportResolver(typeof(LibUsb).Assembly, Resolve);
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DeviceDescriptor
	{
		public byte Length;
		public byte DescriptorType;
		public ushort BcdUsb;
		public byte DeviceClass;
		public byte DeviceSubClass;
		public byte DeviceProtocol;
		public byte MaxPacketSize0;
		public ushort VendorId;
		public ushort ProductId;
		public ushort BcdDevice;
		public byte Manufacturer;
		public byte Product;
		public byte SerialNumber;
		public byte NumConfigurations;
	}

	[DllImport(Library, EntryPoint = "libusb_init")]
	public static extern int Init(out IntPtr context);

	[DllImport(Library, EntryPoint = "libusb_exit")]
	public static extern void Exit(IntPtr context);

	[DllImport(Library, EntryPoint = "libusb_get_device_list")]
	public static extern nint GetDeviceList(IntPtr context, out IntPtr list);

	[DllImport(Library, EntryPoint = "libusb_free_device_list")]
	public static extern void FreeDeviceList(IntPtr list, int unrefDevices);

	[DllImport(Library, EntryPoint = "libusb_get_device_descriptor")]
	public static extern int GetDeviceDescriptor(IntPtr device, out DeviceDescriptor descriptor);

	[DllImport(Library, EntryPoint = "libusb_get_bus_number")]
	public static extern byte GetBusNumber(IntPtr device);

	[DllImport(Library, EntryPoint = "libusb_get_device_address")]
	public static extern byte GetDeviceAddress(IntPtr device);

	[DllImport(Library, EntryPoint = "libusb_open")]
	public static extern int Open(IntPtr device, out IntPtr handle);

	[DllImport(Library, EntryPoint = "libusb_close")]
	public static extern void Close(IntPtr handle);

	[DllImport(Library, EntryPoint = "libusb_set_auto_detach_kernel_driver")]
	public static extern int SetAutoDetachKernelDriver(IntPtr handle, int enable);

	[DllImport(Library, EntryPoint = "libusb_claim_interface")]
	public static extern int ClaimInterface(IntPtr handle, int interfaceNumber);

	[DllImport(Library, EntryPoint = "libusb_release_interface")]
	public static extern int ReleaseInterface(IntPtr handle, int interfaceNumber);

	[DllImport(Library, EntryPoint = "libusb_interrupt_transfer")]
	public static extern int InterruptTransfer(IntPtr handle, byte endpoint, byte[] data, int length, out int transferred, uint timeoutMs);

	[DllImport(Library, EntryPoint = "libusb_error_name")]
	private static extern IntPtr ErrorNamePointer(int code);

	public static string ErrorName(int code) => Marshal.PtrToStringAnsi(ErrorNamePointer(code)) ?? code.ToString();

	/// <summary>
	/// Loads libusb from Wasabi's own binaries folder and from nowhere else, the way HWI carries its own copy:
	/// a library found through the search path would run inside the wallet.
	/// Throws <see cref="DllNotFoundException"/> when it is not there, which keeps the runtime from searching on.
	/// </summary>
	private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if (name != Library)
		{
			return IntPtr.Zero;
		}

		string fileName = OperatingSystem.IsWindows() ? "libusb-1.0.dll"
			: OperatingSystem.IsMacOS() ? "libusb-1.0.0.dylib"
			: "libusb-1.0.so.0";

		// HWI's folder is the one built for the architecture of this process, also on Apple silicon.
		return NativeLibrary.Load(Path.Combine(BundledAppHelpers.GetBinaryFolder(BundledApp.Hwi), fileName));
	}
}
