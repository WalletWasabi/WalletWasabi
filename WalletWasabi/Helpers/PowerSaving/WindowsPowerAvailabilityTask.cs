using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers.PowerSaving;

[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Windows API has its own naming style. Changes would be rather detrimental here.")]
public class WindowsPowerAvailabilityTask : IPowerSavingInhibitorTask
{
	private const int POWER_REQUEST_CONTEXT_VERSION = 0;
	private const int POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;

	[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Not used at the moment.")]
	private const int POWER_REQUEST_CONTEXT_DETAILED_STRING = 0x2;

	private readonly POWER_REQUEST_CONTEXT _context;

	/// <summary>Handle.</summary>
	private readonly IntPtr _request;

	/// <remarks>Guarded by <see cref="_stateLock"/>.</remarks>
	private bool _isDone;

	internal WindowsPowerAvailabilityTask(PowerRequestType requestType, string reason)
	{
		RequestType = requestType;

		// Set up the diagnostic string
		_context.Version = POWER_REQUEST_CONTEXT_VERSION;
		_context.Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING;
		_context.SimpleReasonString = reason;

		_request = PowerCreateRequest(ref _context);

		// Set the request
		if (!PowerSetRequest(_request, requestType))
		{
			Logger.LogError($"Failed to set availability request (last Win32 error: {Marshal.GetLastWin32Error()}).");
			throw new NotImplementedException("Failed to set the availability request. Bailing out.");
		}
	}

	/// <summary>Availability Request Enumerations and Constants</summary>
	/// <seealso href="https://superuser.com/questions/1181186/power-request-types-whats-the-difference-between-display-system-awaymode-p"/>
	public enum PowerRequestType
	{
		/// <summary>The display remains on even if there is no user input for an extended period of time.</summary>
		PowerRequestDisplayRequired = 0,

		/// <summary>The system continues to run instead of entering sleep after a period of user inactivity.</summary>
		PowerRequestSystemRequired = 1,

		/// <summary>The system enters away mode instead of sleep in response to explicit action by the user.</summary>
		/// <remarks>In away mode, the system continues to run but turns off audio and video to give the appearance of sleep.</remarks>
		PowerRequestAwayModeRequired = 2,

		/// <summary>The calling process continues to run instead of being suspended or terminated by process lifetime management mechanisms.</summary>
		/// <remarks>When and how long the process is allowed to run depends on the operating system and power policy settings.</remarks>
		PowerRequestExecutionRequired = 3
	}

	/// <remarks>Guards <see cref="_isDone"/>.</remarks>
	private readonly object _stateLock = new();

	public PowerRequestType RequestType { get; }

	/// <inheritdoc/>
	public bool IsDone
	{
		get
		{
			lock (_stateLock)
			{
				return _isDone;
			}
		}
	}

	/// <summary>Creates a new power request object.</summary>
	/// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powercreaterequest"/>
	[DllImport("kernel32.dll")]
	private static extern IntPtr PowerCreateRequest(ref POWER_REQUEST_CONTEXT Context);

	/// <summary>Increments the count of power requests of the specified type for a power request object.</summary>
	/// <returns>If the function succeeds, it returns a nonzero value. If the function fails, it returns zero.</returns>
	/// <remarks>To get extended error information, call GetLastError.</remarks>
	/// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powersetrequest"/>
	[DllImport("kernel32.dll")]
	private static extern bool PowerSetRequest(IntPtr PowerRequestHandle, PowerRequestType RequestType);

	/// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powerclearrequest"/>
	[DllImport("kernel32.dll")]
	private static extern bool PowerClearRequest(IntPtr PowerRequestHandle, PowerRequestType RequestType);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
	internal static extern int CloseHandle(IntPtr hObject);

	/// <remarks>
	/// Windows defines the POWER_REQUEST_CONTEXT structure with an internal union of <c>SimpleReasonString</c> and Detailed information.
	/// <para>To avoid runtime interop issues, this version of POWER_REQUEST_CONTEXT only supports <c>SimpleReasonString</c>.</para>
	/// <para>To use the detailed information, define the PowerCreateRequest function with the first parameter of type POWER_REQUEST_CONTEXT_DETAILED.</para>
	/// </remarks>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct POWER_REQUEST_CONTEXT
	{
		public uint Version;
		public uint Flags;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string SimpleReasonString;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct PowerRequestContextDetailedInformation
	{
		public IntPtr LocalizedReasonModule;
		public uint LocalizedReasonId;
		public uint ReasonStringCount;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string[] ReasonStrings;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct POWER_REQUEST_CONTEXT_DETAILED
	{
		public uint Version;
		public uint Flags;
		public PowerRequestContextDetailedInformation DetailedInformation;
	}

	/// <param name="reason">Your reason for changing the power settings.</param>
	public static WindowsPowerAvailabilityTask Create(string reason)
	{
		WindowsPowerAvailabilityTask task = new(PowerRequestType.PowerRequestExecutionRequired, reason);
		return task;
	}

	/// <inheritdoc/>
	public bool Prolong(TimeSpan timeSpan)
	{
		// Do nothing.
		return true;
	}

	/// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle"/>
	/// <inheritdoc/>
	public Task StopAsync()
	{
		bool isDone;
		lock (_stateLock)
		{
			isDone = _isDone;
			_isDone = true;
		}

		if (!isDone)
		{
			Logger.LogTrace("Clear the power request.");
			PowerClearRequest(_request, RequestType);

			if (CloseHandle(_request) == 0)
			{
				// This should never happen.
				Logger.LogError($"Failed to close handle (last Win32 error: {Marshal.GetLastWin32Error()}).");
			}
		}
		else
		{
			Logger.LogTrace("Task is already stopped.");
		}

		return Task.CompletedTask;
	}
}
