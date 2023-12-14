using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

internal class AttachmentMessageViewModel : MessageViewModel
{
	public AttachmentMessageViewModel(AttachmentLinks attachmentLinks, ChatMessageMetaData metaData) : base(null, null, metaData)
	{
		Codes = attachmentLinks.Codes;
	}

	public IEnumerable<string> Codes { get; }
}
