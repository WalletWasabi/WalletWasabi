using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.Models;

public abstract class PreferredScriptPubKeyType
{
	private PreferredScriptPubKeyType() { }

	public sealed class Unspecified : PreferredScriptPubKeyType
	{
		public static readonly Unspecified Instance = new ();

		private Unspecified() { }
	}

	public sealed class Specified : PreferredScriptPubKeyType
	{
		public static readonly Specified SegWit = new (ScriptPubKeyType.Segwit);
		public static readonly Specified Taproot = new (ScriptPubKeyType.TaprootBIP86);

		public ScriptPubKeyType ScriptType { get; }
		public string Name => ScriptType.FriendlyName();

		public Specified(ScriptPubKeyType scriptType)
		{
			ScriptType = scriptType;
		}
	}
}
