using HiddenWallet.Helpers;
using HiddenWallet.KeyManagement;
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
using System.Threading;
using static HiddenWallet.QBitNinjaJutsus.QBitNinjaJutsus;
using static HiddenWallet.KeyManagement.Safe;
using static System.Console;
using HiddenWallet.QBitNinjaJutsus;

namespace HiddenWallet
{
	public class Program
	{
		#region Commands
		public static HashSet<string> Commands = new HashSet<string>()
		{
			"help",
			"generate-wallet",
			"recover-wallet",
			"show-balances",
			"show-history",
			"receive",
			"send"
		};
		#endregion

		public static void Main(string[] args)
		{
			//args = new string[] { "help" };
			//args = new string[] { "generate-wallet" };
			//args = new string[] { "generate-wallet", "wallet-file=test2.json" };
			////math super cool donate beach mobile sunny web board kingdom bacon crisp
			////no password
			//args = new string[] { "recover-wallet", "wallet-file=test5.json" };
			//args = new string[] { "show-balances", "wallet-file=test5.json" };
			//args = new string[] { "receive", "wallet-file=test4.json" };
			//args = new string[] { "show-history", "wallet-file=test.json" };
			//args = new string[] { "send", "btc=0.001", "address=mq6fK8fkFyCy9p53m4Gf4fiX2XCHvcwgi1", "wallet-file=test.json" };
			//args = new string[] { "send", "btc=all", "address=mzz63n3n89KVeHQXRqJEVsQX8MZj5zeqCw", "wallet-file=test4.json" };

			// Load config file
			// It also creates it with default settings if doesn't exist
			Config.Load();

			if (args.Length == 0)
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
			foreach (var arg in args.Skip(1))
			{
				if (!arg.Contains('='))
				{
					Exit($"Wrong argument format specified: {arg}");
				}
			}

			#region HelpCommand
			if (command == "help")
			{
				AssertArgumentsLenght(args.Length, 1, 1);
				DisplayHelp();
			}
			#endregion
			#region GenerateWalletCommand
			if (command == "generate-wallet")
			{
				AssertArgumentsLenght(args.Length, 1, 2);
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
				Safe safe = Safe.Create(out mnemonic, pw, walletFilePath, Config.Network);
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
			#endregion
			#region RecoverWalletCommand
			if (command == "recover-wallet")
			{
				AssertArgumentsLenght(args.Length, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				AssertWalletNotExists(walletFilePath);

				WriteLine($"Your software is configured using the Bitcoin {Config.Network} network.");
				WriteLine("Provide your mnemonic words, separated by spaces:");
				var mnemonic = ReadLine();
				AssertCorrectMnemonicFormat(mnemonic);

				WriteLine("Provide your password. Please note the wallet cannot check if your password is correct or not. If you provide a wrong password a wallet will be recovered with your provided mnemonic AND password pair:");
				var password = PasswordConsole.ReadPassword();

				Safe safe = Safe.Recover(mnemonic, password, walletFilePath, Config.Network);
				// If no exception thrown the wallet is successfully recovered.
				WriteLine();
				WriteLine("Wallet is successfully recovered.");
				WriteLine($"Wallet file: {walletFilePath}");
			}
			#endregion
			#region ShowBalancesCommand
			if (command == "show-balances")
			{
				AssertArgumentsLenght(args.Length, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					// 0. Query all operations, grouped by addresses
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses = QueryOperationsPerSafeAddresses(safe, 7);

					// 1. Get all address history record with a wrapper class
					var addressHistoryRecords = new List<AddressHistoryRecord>();
					foreach (var elem in operationsPerAddresses)
					{
						foreach (var op in elem.Value)
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
					foreach (var address in operationsPerAddresses.Keys)
					{
						var recs = new HashSet<AddressHistoryRecord>();
						foreach(var record in addressHistoryRecords)
						{
							if (record.Address == address)
								recs.Add(record);
						}
						addressHistoryRecordsPerAddresses.Add(address, recs);
					}

					// 4. Calculate address balances
					WriteLine();
					WriteLine("---------------------------------------------------------------------------");
					WriteLine("Address\t\t\t\t\tConfirmed\tUnconfirmed");
					WriteLine("---------------------------------------------------------------------------");
					foreach (var elem in addressHistoryRecordsPerAddresses)
					{
						Money confirmedBalance;
						Money unconfirmedBalance;
						GetBalances(elem.Value, out confirmedBalance, out unconfirmedBalance);
						if (confirmedBalance != Money.Zero || unconfirmedBalance != Money.Zero)
							WriteLine($"{elem.Key.ToWif()}\t{confirmedBalance.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")}\t\t{unconfirmedBalance.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")}");
					}
					WriteLine("---------------------------------------------------------------------------");
					WriteLine($"Confirmed Wallet Balance: {confirmedWalletBalance.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")}btc");
					WriteLine($"Unconfirmed Wallet Balance: {unconfirmedWalletBalance.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")}btc");
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
			#endregion
			#region ShowHistoryCommand
			if (command == "show-history")
			{
				AssertArgumentsLenght(args.Length, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					// 0. Query all operations, grouped our used safe addresses
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses = QueryOperationsPerSafeAddresses(safe);

					WriteLine();
					WriteLine("---------------------------------------------------------------------------");
					WriteLine("Date\t\t\tAmount\t\tConfirmed\tTransaction Id");
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

					// 4. Order the records by confirmations and time (Simply time does not work, because of a QBitNinja bug)
					var orderedTxHistoryRecords = txHistoryRecords
						.OrderByDescending(x => x.Item3) // Confirmations
						.ThenBy(x => x.Item1); // FirstSeen
					foreach (var record in orderedTxHistoryRecords)
					{
						// Item2 is the Amount
						if (record.Item2 > 0) ForegroundColor = ConsoleColor.Green;
						else if (record.Item2 < 0) ForegroundColor = ConsoleColor.Red;
						WriteLine($"{record.Item1.DateTime}\t{record.Item2}\t{record.Item3 > 0}\t\t{record.Item4}");
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
			#endregion
			#region ReceiveCommand
			if (command == "receive")
			{
				AssertArgumentsLenght(args.Length, 1, 2);
				var walletFilePath = GetWalletFilePath(args);
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerReceiveAddresses = QueryOperationsPerSafeAddresses(safe, 7, HdPathType.Receive);

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
			#endregion
			#region SendCommand
			if (command == "send")
			{
				AssertArgumentsLenght(args.Length, 3, 4);
				var walletFilePath = GetWalletFilePath(args);
				BitcoinAddress addressToSend;
				try
				{
					addressToSend = BitcoinAddress.Create(GetArgumentValue(args, argName: "address", required: true), Config.Network);
				}
				catch (Exception ex)
				{
					Exit(ex.ToString());
					throw ex;
				}
				Safe safe = DecryptWalletByAskingForPassword(walletFilePath);

				if (Config.ConnectionType == ConnectionType.Http)
				{
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses = QueryOperationsPerSafeAddresses(safe, 7);

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
					Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerChangeAddresses = QueryOperationsPerSafeAddresses(safe, minUnusedKeys: 1, hdPathType: HdPathType.Change);
					foreach (var elem in operationsPerChangeAddresses)
					{
						if (elem.Value.Count == 0)
							changeScriptPubKey = safe.FindPrivateKey(elem.Key).ScriptPubKey;
					}
					if (changeScriptPubKey == null)
						throw new ArgumentNullException();

					// 3. Gather coins can be spend
					WriteLine("Gathering unspent coins...");
					Dictionary<Coin, bool> unspentCoins = GetUnspentCoins(operationsPerNotEmptyPrivateKeys.Keys);

					// 4. Get the fee
					WriteLine("Calculating transaction fee...");
					Money fee;
					try
					{
						var txSizeInBytes = 250;
						using (var client = new HttpClient())
						{

							const string request = @"https://bitcoinfees.21.co/api/v1/fees/recommended";
							var result = client.GetAsync(request, HttpCompletionOption.ResponseContentRead).Result;
							var json = JObject.Parse(result.Content.ReadAsStringAsync().Result);
							var fastestSatoshiPerByteFee = json.Value<decimal>("fastestFee");
							fee = new Money(fastestSatoshiPerByteFee * txSizeInBytes, MoneyUnit.Satoshi);
						}
					}
					catch
					{
						Exit("Couldn't calculate transaction fee, try it again later.");
						throw new Exception("Can't get tx fee");
					}
					WriteLine($"Fee: {fee.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")}btc");

					// 5. How much money we can spend?
					Money availableAmount = Money.Zero;
					Money unconfirmedAvailableAmount = Money.Zero;
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

					// 6. How much to spend?
					Money amountToSend = null;
					string amountString = GetArgumentValue(args, argName: "btc", required: true);
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
						WriteLine($"The transaction fee is {feePc.ToString("0.#")}% of your transaction amount.");
						WriteLine($"Sending:\t {amountToSend.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")}btc");
						WriteLine($"Fee:\t\t {fee.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")}btc");
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
						WriteLine($"In order to complete this transaction you have to spend {unconfirmedToSend.ToDecimal(MoneyUnit.BTC).ToString("0.#############################")} unconfirmed btc.");
						ConsoleKey response = GetYesNoAnswerFromUser();
						if (response == ConsoleKey.N)
						{
							Exit("User interruption.");
						}
					}

					// 8. Select coins
					WriteLine("Selecting coins...");
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

					var qBitClient = new QBitNinjaClient(Config.Network);
					
					// QBit's success response is buggy so let's check manually, too		
					BroadcastResponse broadcastResponse;
					var success = false;
					var tried = 0;
					var maxTry = 7;
					do
					{
						tried++;
						WriteLine($"Try broadcasting transaction... ({tried})");
						broadcastResponse = qBitClient.Broadcast(tx).Result;
						var getTxResp = qBitClient.GetTransaction(tx.GetHash()).Result;
						if (getTxResp == null)
						{
							Thread.Sleep(3000);
							continue;
						}
						else
						{
							success = true;
							break;
						}
					} while (tried <= maxTry);
					if (!success)
					{
						if (broadcastResponse.Error != null)
						{
							WriteLine($"Error code: {broadcastResponse.Error.ErrorCode} Reason: {broadcastResponse.Error.Reason}");
						}
						Exit($"The transaction might not have been successfully broadcasted. Please check the Transaction ID in a block explorer.", ConsoleColor.Blue);
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
			#endregion

			Exit(color: ConsoleColor.Green);
		}

		#region Assertions
		public static void AssertWalletNotExists(string walletFilePath)
		{
			if (File.Exists(walletFilePath))
			{
				Exit($"A wallet, named {walletFilePath} already exists.");
			}
		}
		public static void AssertCorrectNetwork(Network network)
		{
			if (network != Config.Network)
			{
				WriteLine($"The wallet you want to load is on the {network} Bitcoin network.");
				WriteLine($"But your config file specifies {Config.Network} Bitcoin network.");
				Exit();
			}
		}
		public static void AssertCorrectMnemonicFormat(string mnemonic)
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
		// Inclusive
		public static void AssertArgumentsLenght(int length, int min, int max)
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
		#endregion
		#region CommandLineArgumentStuff
		private static string GetArgumentValue(string[] args, string argName, bool required = true)
		{
			string argValue = "";
			foreach (var arg in args)
			{
				if (arg.StartsWith($"{argName}=", StringComparison.OrdinalIgnoreCase))
				{
					argValue = arg.Substring(arg.IndexOf("=") + 1);
					break;
				}
			}
			if (required && argValue == "")
			{
				Exit($@"'{argName}=' is not specified.");
			}
			return argValue;
		}
		private static string GetWalletFilePath(string[] args)
		{
			string walletFileName = GetArgumentValue(args, "wallet-file", required: false);
			if (walletFileName == "") walletFileName = Config.DefaultWalletFileName;

			var walletDirName = "Wallets";
			Directory.CreateDirectory(walletDirName);
			return Path.Combine(walletDirName, walletFileName);
		}
		#endregion
		#region CommandLineInterface
		private static Safe DecryptWalletByAskingForPassword(string walletFilePath)
		{
			Safe safe = null;
			string pw;
			bool correctPw = false;
			WriteLine("Type your password:");
			do
			{
				pw = PasswordConsole.ReadPassword();
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
				WriteLine($"Are you sure you want to proceed? (y/n)");
				response = ReadKey(false).Key;   // true is intercept key (dont show), false is show
				if (response != ConsoleKey.Enter)
					WriteLine();
			} while (response != ConsoleKey.Y && response != ConsoleKey.N);
			return response;
		}
		public static void DisplayHelp()
		{
			WriteLine("Possible commands are:");
			foreach (var cmd in Commands) WriteLine($"\t{cmd}");
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
		#endregion
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
		#endregion
	}
}
