namespace WalletWasabi.Gui.Models
{
	public class WarningMessageWrapper : IMessageWrapper
	{
		public object Message { get; }

		public WarningMessageWrapper(object message)
		{
			Message = message;
		}
	}
}
