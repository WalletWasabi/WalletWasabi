namespace WalletWasabi.Gui.Models
{
	public class ShieldState
	{
		public bool IsPrivacyCriticalVisible { get; }
		public bool IsPrivacySomeVisible { get; }
		public bool IsPrivacyFineVisible { get; }
		public bool IsPrivacyStrongVisible { get; }
		public bool IsPrivacySaiyanVisible { get; }

		public ShieldState(bool isPrivacyCriticalVisible, bool isPrivacySomeVisible, bool isPrivacyFineVisible, bool isPrivacyStrongVisible, bool isPrivacySaiyanVisible = false)
		{
			IsPrivacyCriticalVisible = isPrivacyCriticalVisible;
			IsPrivacySomeVisible = isPrivacySomeVisible;
			IsPrivacyFineVisible = isPrivacyFineVisible;
			IsPrivacyStrongVisible = isPrivacyStrongVisible;
			IsPrivacySaiyanVisible = isPrivacySaiyanVisible;
		}
	}
}
