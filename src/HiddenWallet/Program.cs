using HiddenWallet.Helpers;
using HiddenWallet.KeyManagement;
using HiddenWallet.QBitNinjaJutsus;
using NBitcoin;
using Newtonsoft.Json.Linq;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DotNetTor.SocksPort;
using static HiddenWallet.QBitNinjaJutsus.QBitNinjaJutsus;
using static System.Console;

namespace HiddenWallet
{
	public class Program
	{
		// The minimum number of unused keys those are queried on the blockchain from the HD path.
		private const int MinUnusedKeyNum = 7;
		private static QBitNinjaClient _qBitClient;
		private static HttpClient _httpClient;

		#region Commands

		private static readonly HashSet<string> Commands = new HashSet<string>
		{
			"help",
			"generate-wallet",
			"recover-wallet",
			"show-balances",
			"show-history",
			"show-extkey",
			"show-extpubkey",
			"receive",
			"send"
		};

		#endregion Commands

		private static void Main(string[] args)
		{
			try
			{
				MainAsync(args).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				Error.WriteLine(ex);
			}
		}

		private static async Task MainAsync(IReadOnlyList<string> args)
		{
			//args = new string[] { "help" };
			//args = new string[] { "generate-wallet" };
			//args = new string[] { "generate-wallet", "wallet-file=test2.json" };
			////math super cool donate beach mobile sunny web board kingdom bacon crisp
			////no password
			//args = new string[] { "recover-wallet", "wallet-file=test5.json" };
			//args = new string[] { "show-balances"};
			//args = new string[] { "receive" };
			//args = new string[] { "send","btc=1", "address=mqjVoPiXtLdBdxdqQzWvFSMSBv93swPUUH", "wallet-file=MoliWallet.json" };
			//args = new string[] { "send", "btc=0.1", "address=mkpC5HFC8QHbJbuwajYLDkwPoqcftMU1ga" };
			//args = new string[] { "send", "btc=all", "address=mzz63n3n89KVeHQXRqJEVsQX8MZj5zeqCw", "wallet-file=test4.json" };

			// Load config file
			// It also creates it with default settings if doesn't exist
			Config.Load();

			// Configure QBitNinjaClient
			_qBitClient = new QBitNinjaClient(Config.Network);
			_httpClient = new HttpClient();
			if (Config.UseTor)
			{
				var torHandler = new SocksPortHandler(Config.TorHost, Config.TorSocksPort, ignoreSslCertification: true); // ignoreSslCertification needed for linux, until QBit or DotNetTor fixes its issues
				_qBitClient.SetHttpMessageHandler(torHandler);
				_httpClient = new HttpClient(torHandler);
			}

			if (args.Count == 0)
			{
				DisplayHelp();
				Exit(color: ConsoleColor.Green);
			}
			var command = args[0];
			if (!Commands.Contains(command))
			{
				WriteLine("Wrong command is specified.");
				DisplayHelp();
			}
			foreach (var arg in args.Skip(1).Where(arg => !arg.Contains('=')))
			{
				Exit($"Wrong argument format specified: {arg}");
			}

			#region HelpCommand

			if (command == "help")
			{
				AssertArgumentsLenght(args.Count, 1, 1);
				DisplayHelp();
			}

			#endregion HelpCommand

			#region GenerateWalletCommand

			if (command == "generate-wallet")
			{
				AssertArgumentsLenght(args.Count, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				AssertWalletNotExists(walletFilePath);

				string pw;
				string pwConf;
				do
				{
					// 1. Get password from user
					WriteLine("Choose a password:");
					pw = PasswordConsole.ReadPassword();
					// 2. Get password confirmation from user
					WriteLine("Confirm password:");
					pwConf = PasswordConsole.ReadPassword();

					if (pw != pwConf) WriteLine("Passwords do not match. Try again!");
				} while (pw != pwConf);

				// 3. Create wallet
				string mnemonic;
				Safe.Create(out mnemonic, pw, walletFilePath, Config.Network);
				// If no exception thrown the wallet is successfully created.
				WriteLine();
				WriteLine("Wallet is successfully created.");
				WriteLine($"Wallet file: {walletFilePath}");

				// 4. Display mnemonic
				WriteLine();
				WriteLine("Write down the following mnemonic words.");
				WriteLine("With the mnemonic words AND your password you can recover this wallet by using the recover-wallet command.");
				WriteLine();
				WriteLine("-------");
				WriteLine(mnemonic);
				WriteLine("-------");
			}

			#endregion GenerateWalletCommand

			#region RecoverWalletCommand

			if (command == "recover-wallet")
			{
				AssertArgumentsLenght(args.Count, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				AssertWalletNotExists(walletFilePath);

				WriteLine($"Your software is configured using the Bitcoin {Config.Network} network.");
				WriteLine("Provide your mnemonic words, separated by spaces:");
				var mnemonic = ReadLine();
				AssertCorrectMnemonicFormat(mnemonic);

				WriteLine("Provide your password. Please note the wallet cannot check if your password is correct or not. If you provide a wrong password a wallet will be recovered with your provided mnemonic AND password pair:");
				var password = PasswordConsole.ReadPassword();

				Safe.Recover(mnemonic, password, walletFilePath, Config.Network);
				// If no exception thrown the wallet is successfully recovered.
				WriteLine();
				WriteLine("Wallet is successfully recovered.");
				WriteLine($"Wallet file: {walletFilePath}");
			}

			#endregion RecoverWalletCommand

			#region ShowBalancesCommand

			if (command == "show-balances")
			{
				AssertArgumentsLenght(args.Count, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					await AssertCorrectQBitBlockHeightAsync().ConfigureAwait(false);
					// 0. Query all operations, grouped by addresses
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses = await QueryOperationsPerSafeAddressesAsync(_qBitClient, safe, MinUnusedKeyNum).ConfigureAwait(false);

					// 1. Get all address history record with a wrapper class
					var addressHistoryRecords = new List<AddressHistoryRecord>();
					foreach (var elem in operationsPerAddresses)
					{
						foreach (BalanceOperation op in elem.Value)
						{
							addressHistoryRecords.Add(new AddressHistoryRecord(elem.Key, op));
						}
					}

					// 2. Calculate wallet balances
					Money confirmedWalletBalance;
					Money unconfirmedWalletBalance;
					GetBalances(addressHistoryRecords, out confirmedWalletBalance, out unconfirmedWalletBalance);

					// 3. Group all address history records by addresses
					var addressHistoryRecordsPerAddresses = new Dictionary<BitcoinAddress, HashSet<AddressHistoryRecord>>();
					foreach (BitcoinAddress address in operationsPerAddresses.Keys)
					{
						var recs = new HashSet<AddressHistoryRecord>();
						foreach (AddressHistoryRecord record in addressHistoryRecords)
						{
							if (record.Address == address)
								recs.Add(record);
						}

						addressHistoryRecordsPerAddresses.Add(address, recs);
					}

					// 4. Calculate address balances
					WriteLine();
					WriteLine("---------------------------------------------------------------------------");
					WriteLine(@"Address					Confirmed	Unconfirmed");
					WriteLine("---------------------------------------------------------------------------");
					foreach (var elem in addressHistoryRecordsPerAddresses)
					{
						Money confirmedBalance;
						Money unconfirmedBalance;
						GetBalances(elem.Value, out confirmedBalance, out unconfirmedBalance);
						if (confirmedBalance != Money.Zero || unconfirmedBalance != Money.Zero)
							WriteLine($@"{elem.Key.ToWif()}	{confirmedBalance.ToDecimal(MoneyUnit.BTC):0.#############################}		{unconfirmedBalance.ToDecimal(MoneyUnit.BTC):0.#############################}");
					}

					WriteLine("---------------------------------------------------------------------------");
					WriteLine($"Confirmed Wallet Balance: {confirmedWalletBalance.ToDecimal(MoneyUnit.BTC):0.#############################}btc");
					WriteLine($"Unconfirmed Wallet Balance: {unconfirmedWalletBalance.ToDecimal(MoneyUnit.BTC):0.#############################}btc");
					WriteLine("---------------------------------------------------------------------------");
				}
				else if (Config.ConnectionType == ConnectionType.FullNode)
				{
					throw new NotImplementedException();
				}
				else
				{
					Exit("Invalid connection type.");
				}
			}

			#endregion ShowBalancesCommand

			#region ShowHistoryCommand

			if (command == "show-history")
			{
				AssertArgumentsLenght(args.Count, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					await AssertCorrectQBitBlockHeightAsync().ConfigureAwait(false);
					// 0. Query all operations, grouped our used safe addresses
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses = await QueryOperationsPerSafeAddressesAsync(_qBitClient, safe, MinUnusedKeyNum).ConfigureAwait(false);

					WriteLine();
					WriteLine("---------------------------------------------------------------------------");
					WriteLine(@"Date			Amount		Confirmed	Transaction Id");
					WriteLine("---------------------------------------------------------------------------");

					Dictionary<uint256, List<BalanceOperation>> operationsPerTransactions = GetOperationsPerTransactions(operationsPerAddresses);

					// 3. Create history records from the transactions
					// History records is arbitrary data we want to show to the user
					var txHistoryRecords = new List<Tuple<DateTimeOffset, Money, int, uint256>>();
					foreach (var elem in operationsPerTransactions)
					{
						var amount = Money.Zero;
						foreach (var op in elem.Value)
							amount += op.Amount;

						var firstOp = elem.Value.First();

						txHistoryRecords
							.Add(new Tuple<DateTimeOffset, Money, int, uint256>(
								firstOp.FirstSeen,
								amount,
								firstOp.Confirmations,
								elem.Key));
					}

					// 4. Order the records by confirmations and time (Simply time does not work, because of a QBitNinja issue)
					var orderedTxHistoryRecords = txHistoryRecords
						.OrderByDescending(x => x.Item3) // Confirmations
						.ThenBy(x => x.Item1); // FirstSeen
					foreach (var record in orderedTxHistoryRecords)
					{
						// Item2 is the Amount
						if (record.Item2 > 0) ForegroundColor = ConsoleColor.Green;
						else if (record.Item2 < 0) ForegroundColor = ConsoleColor.DarkGreen;
						WriteLine($@"{record.Item1.DateTime}	{record.Item2}	{record.Item3 > 0}		{record.Item4}");
						ResetColor();
					}
				}
				else if (Config.ConnectionType == ConnectionType.FullNode)
				{
					throw new NotImplementedException();
				}
				else
				{
					Exit("Invalid connection type.");
				}
			}

			#endregion ShowHistoryCommand

			#region ShowExtKeys

			if (command == "show-extkey")
			{
				AssertArgumentsLenght(args.Count, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				WriteLine($"ExtKey: {safe.BitcoinExtKey}");
				WriteLine($"Network: {safe.Network}");
			}
			if (command == "show-extpubkey")
			{
				AssertArgumentsLenght(args.Count, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				WriteLine($"ExtPubKey: {safe.BitcoinExtPubKey}");
				WriteLine($"Network: {safe.Network}");
			}

			#endregion ShowExtKeys

			#region ReceiveCommand

			if (command == "receive")
			{
				AssertArgumentsLenght(args.Count, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					await AssertCorrectQBitBlockHeightAsync().ConfigureAwait(false);
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerReceiveAddresses = await QueryOperationsPerSafeAddressesAsync(_qBitClient, safe, 7, Safe.HdPathType.Receive).ConfigureAwait(false);

					WriteLine("---------------------------------------------------------------------------");
					WriteLine("Unused Receive Addresses");
					WriteLine("---------------------------------------------------------------------------");
					foreach (var elem in operationsPerReceiveAddresses)
						if (elem.Value.Count == 0)
							WriteLine($"{elem.Key.ToWif()}");
				}
				else if (Config.ConnectionType == ConnectionType.FullNode)
				{
					throw new NotImplementedException();
				}
				else
				{
					Exit("Invalid connection type.");
				}
			}

			#endregion ReceiveCommand

			#region SendCommand

			if (command == "send")
			{
				await AssertCorrectQBitBlockHeightAsync().ConfigureAwait(false);
				AssertArgumentsLenght(args.Count, 3, 4);
				var walletFilePath = GetWalletFilePath(args);
				BitcoinAddress addressToSend;
				try
				{
					addressToSend = BitcoinAddress.Create(GetArgumentValue(args, argName: "address", required: true), Config.Network);
				}
				catch (Exception ex)
				{
					Exit(ex.ToString());
					throw;
				}

				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses = await QueryOperationsPerSafeAddressesAsync(_qBitClient, safe, MinUnusedKeyNum).ConfigureAwait(false);

					// 1. Gather all the not empty private keys
					WriteLine("Finding not empty private keys...");
					var operationsPerNotEmptyPrivateKeys = new Dictionary<BitcoinExtKey, List<BalanceOperation>>();
					foreach (var elem in operationsPerAddresses)
					{
						var balance = Money.Zero;
						foreach (var op in elem.Value) balance += op.Amount;

						if (balance > Money.Zero)
						{
							var secret = safe.FindPrivateKey(elem.Key);
							operationsPerNotEmptyPrivateKeys.Add(secret, elem.Value);
						}
					}

					// 2. Get the script pubkey of the change.
					WriteLine("Select change address...");
					Script changeScriptPubKey = null;
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerChangeAddresses = await QueryOperationsPerSafeAddressesAsync(_qBitClient, safe, minUnusedKeys: 1, hdPathType: Safe.HdPathType.Change).ConfigureAwait(false);
					foreach (var elem in operationsPerChangeAddresses)
					{
						if (elem.Value.Count == 0)
							changeScriptPubKey = safe.FindPrivateKey(elem.Key).ScriptPubKey;
					}

					if (changeScriptPubKey == null)
						throw new ArgumentNullException();

					// 3. Gather coins can be spend
					WriteLine("Gathering unspent coins...");
					Dictionary<Coin, bool> unspentCoins = await GetUnspentCoinsAsync(operationsPerNotEmptyPrivateKeys.Keys, _qBitClient).ConfigureAwait(false);

					// 4. How much money we can spend?
					var availableAmount = Money.Zero;
					var unconfirmedAvailableAmount = Money.Zero;
					foreach (var elem in unspentCoins)
					{
						// If can spend unconfirmed add all
						if (Config.CanSpendUnconfirmed)
						{
							availableAmount += elem.Key.Amount;
							if (!elem.Value)
								unconfirmedAvailableAmount += elem.Key.Amount;
						}
						// else only add confirmed ones
						else
						{
							if (elem.Value)
							{
								availableAmount += elem.Key.Amount;
							}
						}
					}

					// 5. Get and calculate fee
					WriteLine("Calculating dynamic transaction fee...");
					Money feePerBytes = null;
					try
					{
						feePerBytes = await QueryFeePerBytesAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						WriteLine(ex.Message);
						Exit("Couldn't calculate transaction fee, try it again later.");
					}

					int inNum;

					string amountString = GetArgumentValue(args, argName: "btc", required: true);
					if (string.Equals(amountString, "all", StringComparison.OrdinalIgnoreCase))
					{
						inNum = unspentCoins.Count;
					}
					else
					{
						const int expectedMinTxSize = 1 * 148 + 2 * 34 + 10 - 1;
						inNum = SelectCoinsToSpend(unspentCoins, ParseBtcString(amountString) + feePerBytes * expectedMinTxSize).Count;
					}
					const int outNum = 2; // 1 address to send + 1 for change
					var estimatedTxSize = inNum * 148 + outNum * 34 + 10 + inNum; // http://bitcoin.stackexchange.com/questions/1195/how-to-calculate-transaction-size-before-sending
					WriteLine($"Estimated tx size: {estimatedTxSize} bytes");
					Money fee = feePerBytes * estimatedTxSize;
					WriteLine($"Fee: {fee.ToDecimal(MoneyUnit.BTC):0.#############################}btc");

					// 6. How much to spend?
					Money amountToSend = null;
					if (string.Equals(amountString, "all", StringComparison.OrdinalIgnoreCase))
					{
						amountToSend = availableAmount;
						amountToSend -= fee;
					}
					else
					{
						amountToSend = ParseBtcString(amountString);
					}

					// 7. Do some checks
					if (amountToSend < Money.Zero || availableAmount < amountToSend + fee)
						Exit("Not enough coins.");

					decimal feePc = Math.Round((100 * fee.ToDecimal(MoneyUnit.BTC)) / amountToSend.ToDecimal(MoneyUnit.BTC));
					if (feePc > 1)
					{
						WriteLine();
						WriteLine($"The transaction fee is {feePc:0.#}% of your transaction amount.");
						WriteLine($"Sending:\t {amountToSend.ToDecimal(MoneyUnit.BTC):0.#############################}btc");
						WriteLine($"Fee:\t\t {fee.ToDecimal(MoneyUnit.BTC):0.#############################}btc");
						ConsoleKey response = GetYesNoAnswerFromUser();
						if (response == ConsoleKey.N)
						{
							Exit("User interruption.");
						}
					}

					var confirmedAvailableAmount = availableAmount - unconfirmedAvailableAmount;
					var totalOutAmount = amountToSend + fee;
					if (confirmedAvailableAmount < totalOutAmount)
					{
						var unconfirmedToSend = totalOutAmount - confirmedAvailableAmount;
						WriteLine();
						WriteLine($"In order to complete this transaction you have to spend {unconfirmedToSend.ToDecimal(MoneyUnit.BTC):0.#############################} unconfirmed btc.");
						ConsoleKey response = GetYesNoAnswerFromUser();
						if (response == ConsoleKey.N)
						{
							Exit("User interruption.");
						}
					}

					// 8. Select coins
					WriteLine("Selecting coins...");
					HashSet<Coin> coinsToSpend = SelectCoinsToSpend(unspentCoins, totalOutAmount);

					// 9. Get signing keys
					var signingKeys = new HashSet<ISecret>();
					foreach (var coin in coinsToSpend)
					{
						foreach (var elem in operationsPerNotEmptyPrivateKeys)
						{
							if (elem.Key.ScriptPubKey == coin.ScriptPubKey)
								signingKeys.Add(elem.Key);
						}
					}

					// 10. Build the transaction
					WriteLine("Signing transaction...");
					var builder = new TransactionBuilder();
					var tx = builder
						.AddCoins(coinsToSpend)
						.AddKeys(signingKeys.ToArray())
						.Send(addressToSend, amountToSend)
						.SetChange(changeScriptPubKey)
						.SendFees(fee)
						.BuildTransaction(true);

					if (!builder.Verify(tx))
						Exit("Couldn't build the transaction.");

					WriteLine($"Transaction Id: {tx.GetHash()}");

					// QBit's success response is buggy so let's check manually, too
					BroadcastResponse broadcastResponse;
					var success = false;
					var tried = 0;
					const int maxTry = 7;
					do
					{
						tried++;
						WriteLine($"Try broadcasting transaction... ({tried})");
						broadcastResponse = await _qBitClient.Broadcast(tx).ConfigureAwait(false);
						var getTxResp = await _qBitClient.GetTransaction(tx.GetHash()).ConfigureAwait(false);
						if (getTxResp != null)
						{
							success = true;
							break;
						}
						else
						{
							await Task.Delay(3000).ConfigureAwait(false);
						}
					} while (tried < maxTry);

					if (!success)
					{
						if (broadcastResponse.Error != null)
						{
							// Try broadcasting with smartbit if QBit fails (QBit issue)
							if (broadcastResponse.Error.ErrorCode == NBitcoin.Protocol.RejectCode.INVALID && broadcastResponse.Error.Reason == "Unknown")
							{
								WriteLine("Try broadcasting transaction with smartbit...");

								var post = "https://testnet-api.smartbit.com.au/v1/blockchain/pushtx";
								if (Config.Network == Network.Main)
									post = "https://api.smartbit.com.au/v1/blockchain/pushtx";

								var content = new StringContent(new JObject(new JProperty("hex", tx.ToHex())).ToString(), Encoding.UTF8, "application/json");
								var resp = await _httpClient.PostAsync(post, content).ConfigureAwait(false);
								var json = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
								if (json.Value<bool>("success"))
									Exit("Transaction is successfully propagated on the network.", ConsoleColor.Green);
								else
									WriteLine($"Error code: {json["error"].Value<string>("code")} Reason: {json["error"].Value<string>("message")}");
							}
							else
								WriteLine($"Error code: {broadcastResponse.Error.ErrorCode} Reason: {broadcastResponse.Error.Reason}");
						}
						Exit("The transaction might not have been successfully broadcasted. Please check the Transaction ID in a block explorer.", ConsoleColor.Blue);
					}
					Exit("Transaction is successfully propagated on the network.", ConsoleColor.Green);
				}
				else if (Config.ConnectionType == ConnectionType.FullNode)
				{
					throw new NotImplementedException();
				}
				else
				{
					Exit("Invalid connection type.");
				}
			}

			#endregion SendCommand

			Exit(color: ConsoleColor.Green);
		}

		private static HashSet<Coin> SelectCoinsToSpend(Dictionary<Coin, bool> unspentCoins, Money totalOutAmount)
		{
			var coinsToSpend = new HashSet<Coin>();
			var unspentConfirmedCoins = new List<Coin>();
			var unspentUnconfirmedCoins = new List<Coin>();
			foreach (var elem in unspentCoins)
				if (elem.Value) unspentConfirmedCoins.Add(elem.Key);
				else unspentUnconfirmedCoins.Add(elem.Key);

			bool haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentConfirmedCoins);
			if (!haveEnough)
				haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentUnconfirmedCoins);
			if (!haveEnough)
				throw new Exception("Not enough funds.");

			return coinsToSpend;
		}

		private static async Task<Money> QueryFeePerBytesAsync()
		{
			try
			{
				HttpResponseMessage response =
					await _httpClient.GetAsync(@"http://api.blockcypher.com/v1/btc/main", HttpCompletionOption.ResponseContentRead)
						.ConfigureAwait(false);

				var json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
				var fastestSatoshiPerByteFee = (int) (json.Value<decimal>("high_fee_per_kb") / 1024);
				var feePerBytes = new Money(fastestSatoshiPerByteFee, MoneyUnit.Satoshi);

				return feePerBytes;
			}
			catch (Exception ex)
			{
				Exit(ex.Message);
				return Money.Zero;
			}
		}

		#region Assertions

		private static void AssertWalletNotExists(string walletFilePath)
		{
			if (File.Exists(walletFilePath))
			{
				Exit($"A wallet, named {walletFilePath} already exists.");
			}
		}

		private static void AssertCorrectNetwork(Network network)
		{
			if (network != Config.Network)
			{
				WriteLine($"The wallet you want to load is on the {network} Bitcoin network.");
				WriteLine($"But your config file specifies {Config.Network} Bitcoin network.");
				Exit();
			}
		}

		private static void AssertCorrectMnemonicFormat(string mnemonic)
		{
			try
			{
				if (new Mnemonic(mnemonic).IsValidChecksum)
					return;
			}
			catch (FormatException) { }
			catch (NotSupportedException) { }

			Exit("Incorrect mnemonic format.");
		}

		private static async Task AssertCorrectQBitBlockHeightAsync()
		{
			var get = "https://testnet-api.smartbit.com.au/v1/blockchain/totals";
			if (Config.Network == Network.Main)
				get = "https://api.smartbit.com.au/v1/blockchain/totals";

			HttpResponseMessage resp;
			try
			{
				resp = await _httpClient.GetAsync(get).ConfigureAwait(false);
			}
			catch
			{
				return; // skip check, chances are qbit and smartbit won't be down the same time
			}

			if (resp == null) return; // skip check, chances are qbit and smartbit won't be down the same time

			var json = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
			if (!json.Value<bool>("success"))
				return; // skip check, chances are qbit and smartbit won't be down the same time
			else
			{
				int lastSmartBitHeight = json["totals"].Value<int>("block_count") - 1;

				var lastBlock =
					await _qBitClient.GetBlock(new BlockFeature(SpecialFeature.Last), headerOnly: true).ConfigureAwait(false);
				int lastQBitHeight = lastBlock.AdditionalInformation.Height;

				if (lastSmartBitHeight <= lastQBitHeight) return;
				else
				{
					await Task.Delay(10000).ConfigureAwait(false);
					lastBlock = await _qBitClient.GetBlock(new BlockFeature(SpecialFeature.Last), headerOnly: true)
						.ConfigureAwait(false);
					lastQBitHeight = lastBlock.AdditionalInformation.Height;

					if (lastSmartBitHeight <= lastQBitHeight) return;
					else
						Exit(
							$"The height of QBitNinja HTTP API is not accurate. Try later. QBit height: {lastQBitHeight} SmartBit height: QBit height: {lastQBitHeight}");

				}
			}
		}

		// Inclusive
		private static void AssertArgumentsLenght(int length, int min, int max)
		{
			if (length < min)
			{
				Exit($"Not enough arguments are specified, minimum: {min}");
			}
			if (length > max)
			{
				Exit($"Too many arguments are specified, maximum: {max}");
			}
		}

		#endregion Assertions

		#region CommandLineArgumentStuff

		private static string GetArgumentValue(IEnumerable<string> args, string argName, bool required = true)
		{
			var argValue = "";
			foreach (string arg in args)
			{
				if (arg.StartsWith($"{argName}=", StringComparison.OrdinalIgnoreCase))
				{
					argValue = arg.Substring(arg.IndexOf("=", StringComparison.Ordinal) + 1);
					break;
				}
			}

			if (required && argValue == "")
			{
				Exit($@"'{argName}=' is not specified.");
			}
			return argValue;
		}

		private static string GetWalletFilePath(IEnumerable<string> args)
		{
			string walletFileName = GetArgumentValue(args, "wallet-file", required: false);
			if (walletFileName == "") walletFileName = Config.DefaultWalletFileName;

			const string walletDirName = "Wallets";
			Directory.CreateDirectory(walletDirName);
			return Path.Combine(walletDirName, walletFileName);
		}

		#endregion CommandLineArgumentStuff

		#region CommandLineInterface

		private static Safe DecryptWalletByAskingForPassword(string walletFilePath)
		{
			Safe safe = null;
			var correctPw = false;
			WriteLine("Type your password:");
			do
			{
				string pw = PasswordConsole.ReadPassword();
				try
				{
					safe = Safe.Load(pw, walletFilePath);
					AssertCorrectNetwork(safe.Network);
					correctPw = true;
				}
				catch (System.Security.SecurityException)
				{
					WriteLine("Invalid password, try again, (or press ctrl+c to exit):");
					correctPw = false;
				}
				catch (Exception ex) when (ex.Message == "WalletFileDoesNotExists")
				{
					Exit($"Wallet file does not exists at {walletFilePath}");
				}
			} while (!correctPw);

			if (safe == null)
				throw new Exception("Wallet could not be decrypted.");

			WriteLine($"{walletFilePath} wallet is decrypted.");
			return safe;
		}

		private static ConsoleKey GetYesNoAnswerFromUser()
		{
			ConsoleKey response;
			do
			{
				WriteLine("Are you sure you want to proceed? (y/n)");
				response = ReadKey(false).Key;   // true is intercept key (dont show), false is show
				if (response != ConsoleKey.Enter)
					WriteLine();
			} while (response != ConsoleKey.Y && response != ConsoleKey.N);

			return response;
		}

		private static void DisplayHelp()
		{
			WriteLine("Possible commands are:");
			foreach (string cmd in Commands) WriteLine($"\t{cmd}");
		}

		public static void Exit(string reason = "", ConsoleColor color = ConsoleColor.Red)
		{
			ForegroundColor = color;
			WriteLine();
			if (reason != "")
			{
				WriteLine(reason);
			}
			WriteLine("Press any key to exit...");
			ResetColor();
			ReadKey();
			Environment.Exit(0);
		}

		#endregion CommandLineInterface

		#region Helpers

		private static Money ParseBtcString(string value)
		{
			decimal amount;
			if (!decimal.TryParse(
						value.Replace(',', '.'),
						NumberStyles.Any,
						CultureInfo.InvariantCulture,
						out amount))
			{
				Exit("Wrong btc amount format.");
			}

			return new Money(amount, MoneyUnit.BTC);
		}

		#endregion Helpers
	}
}