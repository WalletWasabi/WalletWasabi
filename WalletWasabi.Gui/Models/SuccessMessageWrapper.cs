namespace WalletWasabi.Gui.Models
{
	public class SuccessMessageWrapper : IMessageWrapper
	{
		public object Message { get; }

		public SuccessMessageWrapper(object message)
		{
			Message = message;
		}
	}
}
