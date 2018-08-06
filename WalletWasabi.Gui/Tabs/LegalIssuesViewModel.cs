using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class LegalIssuesViewModel : WasabiDocumentTabViewModel
	{
		public LegalIssuesViewModel() : base("Legal Issues")
		{
			LegalIssues = @"
Wasabiwallet.io (Website) provides information and material of a general nature. You are not authorized and nor should you rely on the Website for legal advice, investment advice, or advice of any kind. You act at your own risk in reliance on the contents of the Website. If you make a decision to act or not act, you should contact a licensed attorney in the relevant jurisdiction in which you want or need help. In no way are the owners of, or contributors to, the Website responsible for the actions, decisions, or other behavior taken or not taken by you in reliance upon the Website.

INVESTMENT RISKS
================

The investment in Bitcoin can lead to loss of money over short or even long periods of time. The investors in Bitcoin should expect prices to have large range fluctuations. The information published on the Website cannot guarantee that the investors in Bitcoin would not lose money.

COMPLIANCE WITH TAX OBLIGATIONS
============================

The users of the Website are solely responsible to determinate what, if any, taxes apply to their Bitcoin transactions. The owners of, or contributors to, the Website are NOT responsible for determining the taxes that apply to Bitcoin transactions.

WASABI WALLET DOES NOT STORE, SEND, OR RECEIVE BITCOINS
==============================================

The Wasabi wallet does not store, send or receive bitcoins. This is because Bitcoins exist only by virtue of the ownership record maintained in the Bitcoin network. Any transfer of title in bitcoins occurs within a decentralized Bitcoin network, and not on the Wasabi wallet.

LIMITATION OF LIABILITY
=====================

Unless otherwise required by law, in no event shall the owners of, or contributors to, the Wasabi wallet be liable for any damages of any kind, including, but not limited to, loss of use, loss of profits, or loss of data arising out of or in any way connected with the use of the Website.

WASABI WALLET’S TRADEMARKS
========================

""wasabiwallet.io"", ""Wasabi Wallet"", and all logos related to the Wasabi Wallet services are either trademarks or used as trademarks of Wasabi Wallet like the product of zkSNACKs Ltd. You may not copy, imitate, modify or use them without zkSNAKCKs’ prior written consent. In addition, all page headers, custom graphics, button icons, and scripts are service marks, trademarks, and/or trade dress of Wasabi Wallet. You may not copy, imitate, modify or use them without our prior written consent. You may use HTML logos provided by Wasabi Wallet for the purpose of directing web traffic to the Wasabi Wallet services. You may not alter, modify or change these HTML logos in any way, use them in a manner that mischaracterizes zkSNACKs or the Wasabi Wallet services or display them in any manner that implies zkSNACKs’ sponsorship or endorsement.

COUNTRY OF RESIDENCE
==================

In case if you are a Gibraltar residence person and willing to use Wasabi wallet please inform us in advance on the following e-mail address: legal@zksnacks.com.
			";

			LegalIssues += new string('\n', 100);
		}

		public string LegalIssues { get; }
	}
}
