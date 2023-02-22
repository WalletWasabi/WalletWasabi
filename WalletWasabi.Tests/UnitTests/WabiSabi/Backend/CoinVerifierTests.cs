using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class CoinVerifierTests
{
	// This can be anything but it needs to be https, otherwise we won't even try to send the request.
	public const string TestURL = "https://test.com/test/";

	private WabiSabiConfig _wabisabiTestConfig = new() { RiskFlags = new List<int>() { 11 } };

	[Fact]
	public async Task CanHandleBlacklistedUtxosTestAsync()
	{
		Mock<HttpClient> mockHttpClient = new();
		mockHttpClient.Setup(client => client.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() =>
			{
				string content = GenerateDirtyJsonReport();
				HttpResponseMessage response = new(System.Net.HttpStatusCode.OK);
				response.Content = new StringContent(content);
				return response;
			});

		mockHttpClient.Object.BaseAddress = new Uri(TestURL);

		CoinJoinIdStore coinJoinIdStore = new();
		CoinVerifierApiClient apiClient = new("token", mockHttpClient.Object);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		List<Coin> generatedCoins = GenerateCoins(98);
		List<Coin> naughtyCoins = new();

		ScheduleVerifications(coinVerifier, generatedCoins);
		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			if (item.ShouldBan)
			{
				naughtyCoins.Add(item.Coin);
			}
		}

		Assert.Equal(98, naughtyCoins.Count);
	}

	[Fact]
	public async Task CanFilterNaughtyUtxoTestAsync()
	{
		using HttpResponseMessage dirtyResponse = new(System.Net.HttpStatusCode.OK) { Content = new StringContent(GenerateDirtyJsonReport()) };
		using HttpResponseMessage cleanResponse = new(System.Net.HttpStatusCode.OK) { Content = new StringContent(GenerateCleanJsonReport()) };

		Mock<HttpClient> mockHttpClient = new();
		mockHttpClient.SetupSequence(client => client.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(dirtyResponse)
			.ReturnsAsync(cleanResponse)
			.ReturnsAsync(cleanResponse)
			.ThrowsAsync(new InvalidOperationException())
			.ThrowsAsync(new InvalidOperationException())   // Because of the retry mechanism, we need to fail 3 times to kick out the coin.
			.ThrowsAsync(new InvalidOperationException())
			.ReturnsAsync(cleanResponse)
			.ReturnsAsync(cleanResponse)
			.ReturnsAsync(cleanResponse)
			.ReturnsAsync(cleanResponse)
			.ReturnsAsync(cleanResponse)
			.ReturnsAsync(cleanResponse);

		mockHttpClient.Object.BaseAddress = new Uri(TestURL);

		List<Coin> naughtyCoins = new();
		CoinJoinIdStore coinJoinIdStore = new();
		CoinVerifierApiClient apiClient = new("token", mockHttpClient.Object);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		List<Coin> generatedCoins = GenerateCoins(10);
		List<Coin> removedCoins = new();
		List<Coin> checkedCoins = new();

		ScheduleVerifications(coinVerifier, generatedCoins, TimeSpan.FromSeconds(2));
		coinVerifier.CancelSchedule(generatedCoins[9]);

		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			checkedCoins.Add(item.Coin);
			if (item.ShouldBan)
			{
				naughtyCoins.Add(item.Coin);
			}
			if (item.ShouldRemove)
			{
				removedCoins.Add(item.Coin);
			}
		}

		Assert.Equal(10, checkedCoins.Count);
		Assert.Equal(2, removedCoins.Count);
		Assert.Single(naughtyCoins);
	}

	[Fact]
	public async Task HandleAuthenticationErrorTestAsync()
	{
		Mock<HttpClient> mockHttpClient = new();
		mockHttpClient.Setup(client => client.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() =>
			{
				string content = "{\"error\": \"User roles access forbidden.\" }";
				HttpResponseMessage response = new(System.Net.HttpStatusCode.Forbidden);
				response.Content = new StringContent(content);
				return response;
			});
		mockHttpClient.Object.BaseAddress = new Uri(TestURL);

		List<Coin> naughtyCoins = new();
		CoinJoinIdStore coinJoinIdStore = new();
		CoinVerifierApiClient apiClient = new("token", mockHttpClient.Object);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		List<Coin> generatedCoins = GenerateCoins(5);

		ScheduleVerifications(coinVerifier, generatedCoins);
		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			if (item.ShouldBan)
			{
				naughtyCoins.Add(item.Coin);
			}
		}

		Assert.Empty(naughtyCoins); // Empty, so we won't kick out anyone from the CJ round.
	}

	[Fact]
	public async Task CanHandleAddressReuseAsync()
	{
		using var key = new Key();
		var generatedCoins = new[] {
			WabiSabiFactory.CreateCoin(key, Money.Coins(2m)),
			WabiSabiFactory.CreateCoin(key, Money.Coins(1m))
		};

		Mock<HttpClient> mockHttpClient = new();
		mockHttpClient.Setup(client => client.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() =>
			{
				string content = GenerateCleanJsonReport();
				HttpResponseMessage response = new(System.Net.HttpStatusCode.OK);
				response.Content = new StringContent(content);
				return response;
			});

		mockHttpClient.Object.BaseAddress = new Uri(TestURL);
		CoinJoinIdStore coinJoinIdStore = new();
		CoinVerifierApiClient apiClient = new("token", mockHttpClient.Object);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		ScheduleVerifications(coinVerifier, generatedCoins);
		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			Assert.False(item.ShouldBan);
		}
	}

	[Fact]
	public async Task CanFillWhitelistAfterVerificationTestAsync()
	{
		Mock<HttpClient> mockHttpClient = new();
		mockHttpClient.Setup(client => client.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() =>
			{
				string content = GenerateCleanJsonReport();
				HttpResponseMessage response = new(System.Net.HttpStatusCode.OK);
				response.Content = new StringContent(content);
				return response;
			});

		mockHttpClient.Object.BaseAddress = new Uri(TestURL);

		List<Coin> naughtyCoins = new();
		CoinJoinIdStore coinJoinIdStore = new();
		CoinVerifierApiClient apiClient = new("token", mockHttpClient.Object);
		Whitelist whitelist = new(Enumerable.Empty<Innocent>(), string.Empty, new WabiSabiConfig());
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig, whitelist);

		List<Coin> generatedCoins = GenerateCoins(10);

		ScheduleVerifications(coinVerifier, generatedCoins);

		foreach (CoinVerifyResult result in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			if (result.ShouldBan)
			{
				naughtyCoins.Add(result.Coin);
			}
		}

		Assert.Empty(naughtyCoins); // Empty, so we won't kick out anyone from the CJ round.
		Assert.Equal(10, whitelist.CountInnocents());
	}

	private string GenerateDirtyJsonReport()
	{
		return """{"report_info_section":{"version":"3.4","address":"bc1qpltcdut8lsksq4mr23w6agta2yj6pl2d907zrd","description":"This address has no transaction history.","model_info":{"name":"Coinfirm's default model","uuid":"DEFAULT","version":0},"report_id":"56f058341ce43818cf624a7928a9a6ab8e9727f951224fb47345c526dce57351","address_type":"BTC","address_subtype":"P2WPKH","asset":"BTC","precision":8,"report_type":"standard","report_time":"2022-04-28T11:27:58.271Z","report_block_height":733947,"address_used":true,"is_cluster":false,"early_access":false},"cscore_section":{"cscore":33,"cscore_info":[{"name":"Inactive address","group_name":"Dormant address","impact":100,"type":1,"id":11}]},"profile_section":{},"financial_analysis_section":{"cc_balance":0,"usd_balance":0,"usd_balance_without_tokens":0,"usd_exchange_rate":39657.9},"other_information_section":{"disclaimer":"The Report is information only and is valid on the date of its issuance. Coinfirm does not give any express or implied warranty to the validity of any Report after the date of issuance of any Report.\n\nCoinfirm takes all steps necessary to provide an independent analysis and information in the Report.\n\nCoinfirm is not liable for any changes in assumptions and updates to this report in the case of new facts or circumstances occurring after the date of the Report or not known to Coinfirm at the time of generation of this Report.\n\nAny decision taken by the recipient of this report is made solely on their own risk. The liability of Coinfirm is hereby excluded to the fullest extent permitted by the applicable law. The Report does not discharge any obligation of proper internal risk assessment and/or decision making process.\n\nIn no event will Coinfirm be liable to the recipients for:\n\n   - any act or alleged act, or any omission or alleged omission, that does not constitute wilful misconduct by Coinfirm, as determined in a final, non-appealable judgment by a court of competent jurisdiction,\n   - any indirect, special, punitive, incidental, exemplary, expectancy or consequential damages, including lost profits, lost revenues, loss of opportunity or business interruption, whether or not such damages are foreseeable, or\n   - any third-party claims (whether based in statute, contract, tort or otherwise).\n\nThis report should be read in full because any separate analysis of each of its parts can lead to erroneous conclusions.\n\nCertain information, due to high risk (e.g. crime related), used for analysis, may not be able to be disclosed to the recipient.\n\nTo clarify any aspects contained in the Report please contact us at report@coinfirm.com.","glossary":[{"term":"Address","description":"an address is like a bank account and for example a Bitcoin address starts with either a \u20181\u2019 or a \u20183\u2019 or a \u2018bc1\u2019 and is 26-35 alphanumeric characters in length. The address is generated from the private key, which is required to move assets assigned to this address to another address(es)."},{"term":"Anti-Money Laundering (AML)","description":"the process of systems and controls that are applied to deter, disrupt and detect the flow of illicit value between collusive criminals that represents the proceeds of crimes and predicate offences such as tax evasion, sanctions evasion, theft, counterfeiting and fraud."},{"term":"Blockchain","description":"is a public ledger that records transactions that are performed. This is achieved without any trusted central authority as the maintenance of the blockchain is performed by a network of communicating nodes running the software. Network nodes validate transactions, add them to their copy of the ledger, and then broadcast these ledger additions to other nodes."},{"term":"Combating the Financing of Terrorism (CFT)","description":"the process of deterring and disrupting the financing of terrorism and proliferation. It is increasingly difficult to distinguish from money laundering activity due to the collusive conduct of terrorist financiers and transnational organized criminals, but it is typically distinguished from money laundering on the grounds that the sources of money laundering must be criminal, whereas the sources of finance for terrorism include donations from lawfully earning income. The goal of money laundering is typically a financial gain, while the goal of terrorism financing is typically ideological activity."},{"term":"Customer Due Diligence (CDD)","description":"a process to assess all of the risks associated with a client or relationship, including KYC, and that requires that the overall client conduct, and transactions are assessed to determine if this is unusual and reportable. CDD requires that obliged entities assess the risks before entering in to a relationship, and continuously thereafter in response to trigger events or suspicious activity for example. It is a continual process that is designed to assess and monitor changes in customer risks."},{"term":"Decentralised Virtual Currencies","description":"(cryptocurrencies) are distributed, open-source, mathematically-based peer-to-peer virtual currencies that have no central administering authority, and no central monitoring or oversight. Examples include: Bitcoin, Ethereum, Litecoin and Namecoin."},{"term":"Distributed Ledger (Shared Ledger)","description":"\u2018Ledgers\u2019, or put simply, records of activity, were historically maintained on paper, more recently these were transferred to bytes on computers, and are now supported by algorithms in blockchains. They are essentially an asset database that can be shared across a network of multiple sites, geographies or institutions. All participants within a network can have their own identical copy of the ledger. Any changes to the ledger are reflected in all copies in minutes, or in some cases, seconds. The assets can be financial, legal, physical or electronic. The security and accuracy of the assets stored in the ledger are maintained cryptographically using \u2018keys\u2019 and signatures to control who can do what within the shared ledger. Entries can also be updated by one, some or all of the participants, according to rules agreed by the network. (Taken from UK Government: \u2018Distributed Ledger Technology: beyond block chain\u2019)."},{"term":"Electronic money (e-money)","description":"is an electronic store of monetary value, based on technological mechanism for holding and accessing fiat currency."},{"term":"Enhanced Customer Due Diligence (EDD)","description":"a higher standard of due diligence, including identity verification and investigation that is required to be performed for those clients and relationships that have been identified as presenting the greatest risk of financial crimes. These risks include among others PEPs, Correspondent Banking, non-face-to-face activities such as virtual currency and private banking."},{"term":"Exchanger / virtual currency exchange","description":"is a website service, or an entity, engaged as a business in the exchange of virtual currency for real currency, funds, or other forms of virtual currency and also precious metals, and vice versa, for a fee (commission). Exchangers generally accept a wide range of payments, including cash, wire payments, credit cards, and other virtual currencies. Individuals typically use exchangers to deposit and withdraw money from virtual currency accounts. Examples include: Bitstamp, GDAX, Kraken, OKCoin and ItBit."},{"term":"Fiat Currency","description":"is legal tender that is backed by the central government who issued it. Examples are the US Dollar, Japanese Yen and UK Sterling."},{"term":"\u2018Fifth\u2019 EU Money Laundering Directive (5MLD)","description":"an amendment to the 4MLD that was agreed in response to the terrorist attacks across Europe in 2015 and 2016. The new law must be transposed by member states by 10th January 2020, and new measures include the requirement for virtual currency exchange services and virtual currency custodian wallet providers to be treated as \u2018obliged entities\u2019."},{"term":"FinTech","description":"refers to new applications, processes, products or business models that are being applied to improve the efficiency and security of financial services."},{"term":"Fourth EU Money Laundering Directive (4MLD)","description":"European response to the FATF 40 Recommendations from February 2012 and was required to be transposed by EU member states by 26th June 2017."},{"term":"Hash","description":"A hash value (or simply hash), also called a message digest, is a string of characters generated from a string of digital data, e.g. a pdf file. The hash is substantially smaller than the text itself and is generated by a formula in such a way that it is extremely unlikely that some other text will produce the same hash value and it is extremely difficult to reverse to identify the source message."},{"term":"Know Your Customer (KYC)","description":"the identification and verification of the natural person, legal entity or legal arrangement through identifying information, such as name and address, and the verification of these details to identify fraud, misrepresentation etc."},{"term":"Money Laundering","description":"a process to disguise the illicit source of value, either by self-laundering or through the placement, layering or integration process, conducted by criminals who ultimately wish to use this value for self-gratification, or to continue to finance their illicit activities."},{"term":"Money Laundering Reporting Officer (MLRO)","description":"the chief compliance officer responsible for all AML/CFT activities and responsible for ensuring that an obliged entity is not used by criminal or the financiers of terrorism."},{"term":"Nodes","description":"are computers in the blockchain network which receive new transactions and blocks, validate these transactions and blocks and spread valid transactions and blocks to connected nodes and ignore invalid transactions and blocks. It is generally considered that the more nodes exist in the network, the more secure the is the system."},{"term":"Politically Exposed Person (PEP)","description":"a person of high public office who may be able to influence the misappropriation of public funds whilst in office, or the awarding of public contracts. Include members of government, ruling classes such as Presidents, Royalty, Ministers of the Government and military and judiciary. The families of PEPs, and their close business associates, are also included due to the close affinity and trust that they may enjoy in their relationship, and which may lead to the PEP using these relationships as \u2018front\u2019 or \u2018informal\u2019 nominees."},{"term":"Private Key","description":"a private key is a cryptographic code that functions as a secret password that allows the user to sign a cryptocurrency transaction and transfer funds to another cryptocurrency address. Using the private key proves ownership of cryptocurrency."},{"term":"Sanctions","description":"when applied to financial services, represent a prohibition on providing regulated services to the subject of the sanction, and the requirement to freeze and report any assets that are held to the local jurisdiction sanctions administrator, such as OFAC or HMT."},{"term":"Simplified Due Diligence (SDD)","description":"a lower level of customer due diligence verification that can be performed where there is no, or a lesser, risk of money laundering."},{"term":"Trading platforms","description":"function as marketplaces, bringing together buyers and sellers of virtual currencies by providing them with a platform on which they can offer and bid among themselves. In contrast to exchanges, the trading platforms do not engage in the buying and selling themselves. Some trading platforms give their customers the option of locating potential customers nearby. Examples include LocalBitcoins.com and Mycelium Local Trader."},{"term":"Transaction Fee","description":"Is earned by miners when a transaction is completed. The minimum transaction fee required is determined by the \"size\" (kilobytes) of the transaction data. Most small transactions require a fee of about 0.0001 BTC and transactions with larger fees are given priority to be added to the block, so they are usually confirmed faster than transactions with low fees."}]}}""";
	}

	private string GenerateCleanJsonReport()
	{
		return """{"report_info_section":{"version":"3.4","address":"bc1qpltcdut8lsksq4mr23w6agta2yj6pl2d907zrd","description":"This address has no transaction history.","model_info":{"name":"Coinfirm's default model","uuid":"DEFAULT","version":0},"report_id":"56f058341ce43818cf624a7928a9a6ab8e9727f951224fb47345c526dce57351","address_type":"BTC","address_subtype":"P2WPKH","asset":"BTC","precision":8,"report_type":"standard","report_time":"2022-04-28T11:27:58.271Z","report_block_height":733947,"address_used":true,"is_cluster":false,"early_access":false},"cscore_section":{"cscore":33,"cscore_info":[{"name":"Inactive address","group_name":"Dormant address","impact":100,"type":1,"id":100}]},"profile_section":{},"financial_analysis_section":{"cc_balance":0,"usd_balance":0,"usd_balance_without_tokens":0,"usd_exchange_rate":39657.9},"other_information_section":{"disclaimer":"The Report is information only and is valid on the date of its issuance. Coinfirm does not give any express or implied warranty to the validity of any Report after the date of issuance of any Report.\n\nCoinfirm takes all steps necessary to provide an independent analysis and information in the Report.\n\nCoinfirm is not liable for any changes in assumptions and updates to this report in the case of new facts or circumstances occurring after the date of the Report or not known to Coinfirm at the time of generation of this Report.\n\nAny decision taken by the recipient of this report is made solely on their own risk. The liability of Coinfirm is hereby excluded to the fullest extent permitted by the applicable law. The Report does not discharge any obligation of proper internal risk assessment and/or decision making process.\n\nIn no event will Coinfirm be liable to the recipients for:\n\n   - any act or alleged act, or any omission or alleged omission, that does not constitute wilful misconduct by Coinfirm, as determined in a final, non-appealable judgment by a court of competent jurisdiction,\n   - any indirect, special, punitive, incidental, exemplary, expectancy or consequential damages, including lost profits, lost revenues, loss of opportunity or business interruption, whether or not such damages are foreseeable, or\n   - any third-party claims (whether based in statute, contract, tort or otherwise).\n\nThis report should be read in full because any separate analysis of each of its parts can lead to erroneous conclusions.\n\nCertain information, due to high risk (e.g. crime related), used for analysis, may not be able to be disclosed to the recipient.\n\nTo clarify any aspects contained in the Report please contact us at report@coinfirm.com.","glossary":[{"term":"Address","description":"an address is like a bank account and for example a Bitcoin address starts with either a \u20181\u2019 or a \u20183\u2019 or a \u2018bc1\u2019 and is 26-35 alphanumeric characters in length. The address is generated from the private key, which is required to move assets assigned to this address to another address(es)."},{"term":"Anti-Money Laundering (AML)","description":"the process of systems and controls that are applied to deter, disrupt and detect the flow of illicit value between collusive criminals that represents the proceeds of crimes and predicate offences such as tax evasion, sanctions evasion, theft, counterfeiting and fraud."},{"term":"Blockchain","description":"is a public ledger that records transactions that are performed. This is achieved without any trusted central authority as the maintenance of the blockchain is performed by a network of communicating nodes running the software. Network nodes validate transactions, add them to their copy of the ledger, and then broadcast these ledger additions to other nodes."},{"term":"Combating the Financing of Terrorism (CFT)","description":"the process of deterring and disrupting the financing of terrorism and proliferation. It is increasingly difficult to distinguish from money laundering activity due to the collusive conduct of terrorist financiers and transnational organized criminals, but it is typically distinguished from money laundering on the grounds that the sources of money laundering must be criminal, whereas the sources of finance for terrorism include donations from lawfully earning income. The goal of money laundering is typically a financial gain, while the goal of terrorism financing is typically ideological activity."},{"term":"Customer Due Diligence (CDD)","description":"a process to assess all of the risks associated with a client or relationship, including KYC, and that requires that the overall client conduct, and transactions are assessed to determine if this is unusual and reportable. CDD requires that obliged entities assess the risks before entering in to a relationship, and continuously thereafter in response to trigger events or suspicious activity for example. It is a continual process that is designed to assess and monitor changes in customer risks."},{"term":"Decentralised Virtual Currencies","description":"(cryptocurrencies) are distributed, open-source, mathematically-based peer-to-peer virtual currencies that have no central administering authority, and no central monitoring or oversight. Examples include: Bitcoin, Ethereum, Litecoin and Namecoin."},{"term":"Distributed Ledger (Shared Ledger)","description":"\u2018Ledgers\u2019, or put simply, records of activity, were historically maintained on paper, more recently these were transferred to bytes on computers, and are now supported by algorithms in blockchains. They are essentially an asset database that can be shared across a network of multiple sites, geographies or institutions. All participants within a network can have their own identical copy of the ledger. Any changes to the ledger are reflected in all copies in minutes, or in some cases, seconds. The assets can be financial, legal, physical or electronic. The security and accuracy of the assets stored in the ledger are maintained cryptographically using \u2018keys\u2019 and signatures to control who can do what within the shared ledger. Entries can also be updated by one, some or all of the participants, according to rules agreed by the network. (Taken from UK Government: \u2018Distributed Ledger Technology: beyond block chain\u2019)."},{"term":"Electronic money (e-money)","description":"is an electronic store of monetary value, based on technological mechanism for holding and accessing fiat currency."},{"term":"Enhanced Customer Due Diligence (EDD)","description":"a higher standard of due diligence, including identity verification and investigation that is required to be performed for those clients and relationships that have been identified as presenting the greatest risk of financial crimes. These risks include among others PEPs, Correspondent Banking, non-face-to-face activities such as virtual currency and private banking."},{"term":"Exchanger / virtual currency exchange","description":"is a website service, or an entity, engaged as a business in the exchange of virtual currency for real currency, funds, or other forms of virtual currency and also precious metals, and vice versa, for a fee (commission). Exchangers generally accept a wide range of payments, including cash, wire payments, credit cards, and other virtual currencies. Individuals typically use exchangers to deposit and withdraw money from virtual currency accounts. Examples include: Bitstamp, GDAX, Kraken, OKCoin and ItBit."},{"term":"Fiat Currency","description":"is legal tender that is backed by the central government who issued it. Examples are the US Dollar, Japanese Yen and UK Sterling."},{"term":"\u2018Fifth\u2019 EU Money Laundering Directive (5MLD)","description":"an amendment to the 4MLD that was agreed in response to the terrorist attacks across Europe in 2015 and 2016. The new law must be transposed by member states by 10th January 2020, and new measures include the requirement for virtual currency exchange services and virtual currency custodian wallet providers to be treated as \u2018obliged entities\u2019."},{"term":"FinTech","description":"refers to new applications, processes, products or business models that are being applied to improve the efficiency and security of financial services."},{"term":"Fourth EU Money Laundering Directive (4MLD)","description":"European response to the FATF 40 Recommendations from February 2012 and was required to be transposed by EU member states by 26th June 2017."},{"term":"Hash","description":"A hash value (or simply hash), also called a message digest, is a string of characters generated from a string of digital data, e.g. a pdf file. The hash is substantially smaller than the text itself and is generated by a formula in such a way that it is extremely unlikely that some other text will produce the same hash value and it is extremely difficult to reverse to identify the source message."},{"term":"Know Your Customer (KYC)","description":"the identification and verification of the natural person, legal entity or legal arrangement through identifying information, such as name and address, and the verification of these details to identify fraud, misrepresentation etc."},{"term":"Money Laundering","description":"a process to disguise the illicit source of value, either by self-laundering or through the placement, layering or integration process, conducted by criminals who ultimately wish to use this value for self-gratification, or to continue to finance their illicit activities."},{"term":"Money Laundering Reporting Officer (MLRO)","description":"the chief compliance officer responsible for all AML/CFT activities and responsible for ensuring that an obliged entity is not used by criminal or the financiers of terrorism."},{"term":"Nodes","description":"are computers in the blockchain network which receive new transactions and blocks, validate these transactions and blocks and spread valid transactions and blocks to connected nodes and ignore invalid transactions and blocks. It is generally considered that the more nodes exist in the network, the more secure the is the system."},{"term":"Politically Exposed Person (PEP)","description":"a person of high public office who may be able to influence the misappropriation of public funds whilst in office, or the awarding of public contracts. Include members of government, ruling classes such as Presidents, Royalty, Ministers of the Government and military and judiciary. The families of PEPs, and their close business associates, are also included due to the close affinity and trust that they may enjoy in their relationship, and which may lead to the PEP using these relationships as \u2018front\u2019 or \u2018informal\u2019 nominees."},{"term":"Private Key","description":"a private key is a cryptographic code that functions as a secret password that allows the user to sign a cryptocurrency transaction and transfer funds to another cryptocurrency address. Using the private key proves ownership of cryptocurrency."},{"term":"Sanctions","description":"when applied to financial services, represent a prohibition on providing regulated services to the subject of the sanction, and the requirement to freeze and report any assets that are held to the local jurisdiction sanctions administrator, such as OFAC or HMT."},{"term":"Simplified Due Diligence (SDD)","description":"a lower level of customer due diligence verification that can be performed where there is no, or a lesser, risk of money laundering."},{"term":"Trading platforms","description":"function as marketplaces, bringing together buyers and sellers of virtual currencies by providing them with a platform on which they can offer and bid among themselves. In contrast to exchanges, the trading platforms do not engage in the buying and selling themselves. Some trading platforms give their customers the option of locating potential customers nearby. Examples include LocalBitcoins.com and Mycelium Local Trader."},{"term":"Transaction Fee","description":"Is earned by miners when a transaction is completed. The minimum transaction fee required is determined by the \"size\" (kilobytes) of the transaction data. Most small transactions require a fee of about 0.0001 BTC and transactions with larger fees are given priority to be added to the block, so they are usually confirmed faster than transactions with low fees."}]}}""";
	}

	private List<Coin> GenerateCoins(int numToGen)
	{
		List<Coin> coins = new();
		for (int i = 0; i < numToGen; i++)
		{
			using Key key = new();
			coins.Add(WabiSabiFactory.CreateCoin(key));
		}

		return coins;
	}

	private void ScheduleVerifications(CoinVerifier coinVerifier, IEnumerable<Coin> coins, TimeSpan? delay = null)
	{
		foreach (Coin coin in coins)
		{
			coinVerifier.TryScheduleVerification(coin, out _, CancellationToken.None, delay, confirmations: _wabisabiTestConfig.CoinVerifierRequiredConfirmations);
		}
	}
}
