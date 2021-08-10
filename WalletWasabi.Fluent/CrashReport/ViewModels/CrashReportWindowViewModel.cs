using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.CrashReport.ViewModels
{
	public partial class CrashReportWindowViewModel : ViewModelBase
	{
		[AutoNotify] private SerializableException _serializableException;
		[AutoNotify] private string _logPath;

		public CrashReportWindowViewModel(SerializableException exception, string logPath)
		{
			SerializableException = exception;
			LogPath = logPath;
		}

		public string Details => $"A problem has occurred and Wasabi is unable to continue.";
		public string Message => $"{_serializableException.Message}";
		public string Title => $"{_serializableException.Message}";
	}
}
