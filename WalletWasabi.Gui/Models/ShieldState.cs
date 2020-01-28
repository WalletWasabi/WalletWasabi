namespace WalletWasabi.Gui.Models
{
	public struct ShieldState
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

		public override bool Equals(object obj)
		{
			if (obj is ShieldState state)
			{
				if (state.GetHashCode() == GetHashCode())
				{
					return true;
				}
			}

			return false;
		}

		public override int GetHashCode()
		{
			var items = new[]
			{
				IsPrivacyCriticalVisible,
				IsPrivacySomeVisible,
				IsPrivacyFineVisible,
				IsPrivacyStrongVisible,
				IsPrivacySaiyanVisible
			};

			uint result = 0;
			for (int i = 0; i < items.Length; i++)
			{
				result |= (uint)(items[i] ? 1 : 0) << i;
			}
			return result.GetHashCode();
		}

		public static bool operator ==(ShieldState left, ShieldState right) => left.Equals(right);

		public static bool operator !=(ShieldState left, ShieldState right) => !(left == right);
	}
}
