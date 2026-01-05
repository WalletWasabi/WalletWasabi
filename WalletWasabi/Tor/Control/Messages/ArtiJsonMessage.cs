namespace WalletWasabi.Tor.Control.Messages;

public record ArtiJsonMessage(string Json) : ITorControlReply
{
}
