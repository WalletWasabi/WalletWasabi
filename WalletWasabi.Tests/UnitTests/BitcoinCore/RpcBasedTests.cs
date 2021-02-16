using NBitcoin;
using NBitcoin.RPC;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class RpcBasedTests
	{
		#region Mocked RPC response

		public const string RpcOutput = @"
		{
		    'hash': '27cac34bec2bfc3422c352d558b4db29e6d7e8114db2dbc955df06a63cda82fe',
		    'confirmations': 1,
		    'strippedsize': 442,
		    'size': 478,
		    'weight': 1804,
		    'height': 102,
		    'version': 536870912,
		    'versionHex': '20000000',
		    'merkleroot': 'ddd7eab214fe2dc1f875ba0087b3dee60c5e55876d1494eacea88f259204004a',
		    'tx': [
		      {
		        'txid': '5d95a076a2231feae22dfcf10285bd4069e6ca4e7e2a896a266e17e6807d8d8c',
		        'hash': 'b9cde924c05e72afaba40d03ea91b01fdccb976a119d9f3de57d5e2eb3f46006',
		        'version': 2,
		        'size': 172,
		        'vsize': 145,
		        'weight': 580,
		        'locktime': 0,
		        'vin': [
		          {
		            'coinbase': '01660101',
		            'sequence': 4294967295
		          }
		        ],
		        'vout': [
		          {
		            'value': 50.00004500,
		            'n': 0,
		            'scriptPubKey': {
		              'asm': 'OP_DUP OP_HASH160 381907cb00a047109bc340afe06504d67472d3de OP_EQUALVERIFY OP_CHECKSIG',
		              'desc': 'addr(mkda7wbQ9nVQa8ayYbTVifVzNie1kf8gKy)#yzcmfs2s',
		              'hex': '76a914381907cb00a047109bc340afe06504d67472d3de88ac',
		              'reqSigs': 1,
		              'type': 'pubkeyhash',
		              'addresses': [
		                'mkda7wbQ9nVQa8ayYbTVifVzNie1kf8gKy'
		              ]
		            }
		          },
		          {
		            'value': 0.00000000,
		            'n': 1,
		            'scriptPubKey': {
		              'asm': 'OP_RETURN aa21a9ed8198d32b4242fa8a0bd0ae04903f602d33a6f92e768da643ad3b72ad9ce72a06',
		              'desc': 'raw(6a24aa21a9ed8198d32b4242fa8a0bd0ae04903f602d33a6f92e768da643ad3b72ad9ce72a06)#87566l0s',
		              'hex': '6a24aa21a9ed8198d32b4242fa8a0bd0ae04903f602d33a6f92e768da643ad3b72ad9ce72a06',
		              'type': 'nulldata'
		            }
		          }
		        ],
		        'hex': '020000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff0401660101ffffffff029403062a010000001976a914381907cb00a047109bc340afe06504d67472d3de88ac0000000000000000266a24aa21a9ed8198d32b4242fa8a0bd0ae04903f602d33a6f92e768da643ad3b72ad9ce72a060120000000000000000000000000000000000000000000000000000000000000000000000000'
		      },
		      {
		        'txid': 'f5a2f2747dc8c2ba9d362ef3c47400b01586a811fd0d0003549bce54b5c51ed4',
		        'hash': 'f5a2f2747dc8c2ba9d362ef3c47400b01586a811fd0d0003549bce54b5c51ed4',
		        'version': 2,
		        'size': 225,
		        'vsize': 225,
		        'weight': 900,
		        'locktime': 101,
		        'vin': [
		          {
		            'txid': '4815e72e2d967b666097c476473d0175b94d2a22f384e6389ab44dc9260dd8e0',
		            'vout': 0,
		            'scriptSig': {
		              'asm': '30440220242cb6ccdfa7a4f83b3226b6694af52a9eafc94c7640a89786ffc93a07d79cd3022051375bc352b1f96223523e262ab93d9081135edafffd8e03a4fd38f49150e9b9[ALL] 02302fc55898d0b2adaf49be6c17c5804651ddb8ee114a05eb9da0a9517b8bccef',
		              'hex': '4730440220242cb6ccdfa7a4f83b3226b6694af52a9eafc94c7640a89786ffc93a07d79cd3022051375bc352b1f96223523e262ab93d9081135edafffd8e03a4fd38f49150e9b9012102302fc55898d0b2adaf49be6c17c5804651ddb8ee114a05eb9da0a9517b8bccef'
		            },
		            'prevout': {
		              'height': 1,
		              'value': 50.00000000,
		              'generated': true,
		              'scriptPubKey': {
		                'asm': 'OP_DUP OP_HASH160 381907cb00a047109bc340afe06504d67472d3de OP_EQUALVERIFY OP_CHECKSIG',
		                'desc': 'addr(mkda7wbQ9nVQa8ayYbTVifVzNie1kf8gKy)#yzcmfs2s',
		                'hex': '76a914381907cb00a047109bc340afe06504d67472d3de88ac',
		                'reqSigs': 1,
		                'type': 'pubkeyhash',
		                'addresses': [
		                  'mkda7wbQ9nVQa8ayYbTVifVzNie1kf8gKy'
		                ]
		              }
		            },
		            'sequence': 4294967294
		          }
		        ],
		        'vout': [
		          {
		            'value': 48.99995500,
		            'n': 0,
		            'scriptPubKey': {
		              'asm': 'OP_DUP OP_HASH160 6028ad75c715247d9179946458f946de0b83d3db OP_EQUALVERIFY OP_CHECKSIG',
		              'desc': 'addr(mpHPtoCqC8XJkCbRAoDfJFk8Uiidov8JCd)#f0l9dcff',
		              'hex': '76a9146028ad75c715247d9179946458f946de0b83d3db88ac',
		              'reqSigs': 1,
		              'type': 'pubkeyhash',
		              'addresses': [
		                'mpHPtoCqC8XJkCbRAoDfJFk8Uiidov8JCd'
		              ]
		            }
		          },
		          {
		            'value': 1.00000000,
		            'n': 1,
		            'scriptPubKey': {
		              'asm': 'OP_DUP OP_HASH160 29f5bf0598ecef7ae4f9f1163cdeecf1182c51f9 OP_EQUALVERIFY OP_CHECKSIG',
		              'desc': 'addr(mjLpPfQNYKCJGc1qXyU71wr6vt9yuVPLR6)#4ezwynfz',
		              'hex': '76a91429f5bf0598ecef7ae4f9f1163cdeecf1182c51f988ac',
		              'reqSigs': 1,
		              'type': 'pubkeyhash',
		              'addresses': [
		                'mjLpPfQNYKCJGc1qXyU71wr6vt9yuVPLR6'
		              ]
		            }
		          }
		        ],
		        'fee': 0.00004500,
		        'hex': '0200000001e0d80d26c94db49a38e684f3222a4db975013d4776c49760667b962d2ee71548000000006a4730440220242cb6ccdfa7a4f83b3226b6694af52a9eafc94c7640a89786ffc93a07d79cd3022051375bc352b1f96223523e262ab93d9081135edafffd8e03a4fd38f49150e9b9012102302fc55898d0b2adaf49be6c17c5804651ddb8ee114a05eb9da0a9517b8bcceffeffffff026cff0f24010000001976a9146028ad75c715247d9179946458f946de0b83d3db88ac00e1f505000000001976a91429f5bf0598ecef7ae4f9f1163cdeecf1182c51f988ac65000000'
		      }
		    ],
		    'time': 1583444802,
		    'mediantime': 1583444739,
		    'nonce': 1,
		    'bits': '207fffff',
		    'difficulty': 4.656542373906925e-10,
		    'chainwork': '00000000000000000000000000000000000000000000000000000000000000ce',
		    'nTx': 2,
		    'previousblockhash': '1d434df0cdd3fe26535ebe9734ef013b036441be38921606a9336ce74ab1cf04'
		}";

		#endregion Mocked RPC response

		[Fact]
		public async Task AllFeeEstimateAsync()
		{
			using var services = new HostedServices();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync(CancellationToken.None);
			try
			{
				var rpc = coreNode.RpcClient;
				var estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, simulateIfRegTest: true);
				Assert.Equal(7, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Conservative, estimations.Type);
				estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: true);
				Assert.Equal(7, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Economical, estimations.Type);
			}
			finally
			{
				await services.StopAllAsync(CancellationToken.None);
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task CantDoubleSpendAsync()
		{
			using var services = new HostedServices();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync(CancellationToken.None);
			try
			{
				var rpc = coreNode.RpcClient;
				var network = rpc.Network;

				var walletName = "wallet.dat";
				await rpc.CreateWalletAsync(walletName);

				var key = new Key();
				var blockId = await rpc.GenerateToAddressAsync(1, key.PubKey.WitHash.GetAddress(network));
				var block = await rpc.GetBlockAsync(blockId[0]);
				var coinBaseTx = block.Transactions[0];

				var tx = Transaction.Create(network);
				tx.Inputs.Add(coinBaseTx, 0);
				tx.Outputs.Add(Money.Coins(49.9999m), new Key().PubKey.WitHash.GetAddress(network));
				tx.Sign(key.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
				var valid = tx.Check();

				var doubleSpend = Transaction.Create(network);
				doubleSpend.Inputs.Add(coinBaseTx, 0);
				doubleSpend.Outputs.Add(Money.Coins(49.998m), new Key().PubKey.WitHash.GetAddress(network));
				doubleSpend.Sign(key.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
				valid = doubleSpend.Check();

				await rpc.GenerateAsync(101);

				var txId = await rpc.SendRawTransactionAsync(tx);
				await Assert.ThrowsAsync<RPCException>(async () => await rpc.SendRawTransactionAsync(doubleSpend));
			}
			finally
			{
				await services.StopAllAsync(CancellationToken.None);
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public async Task VerboseBlockInfoAsync()
		{
			using var services = new HostedServices();
			var coreNode = await TestNodeBuilder.CreateAsync(services);
			await services.StartAllAsync(CancellationToken.None);
			try
			{
				var rpc = coreNode.RpcClient;
				var blockInfo = await rpc.GetVerboseBlockAsync(coreNode.Network.GenesisHash);
				Assert.NotNull(blockInfo.Transactions.ElementAt(0).Inputs.ElementAt(0).Coinbase);
			}
			finally
			{
				await services.StopAllAsync(CancellationToken.None);
				await coreNode.TryStopAsync();
			}
		}

		[Fact]
		public void ParseVerboseBlockInfo()
		{
			var blockInfo = RpcParser.ParseVerboseBlockResponse(RpcOutput);
			Assert.Equal(2, blockInfo.Transactions.Count());
			Assert.Single(blockInfo.Transactions.ElementAt(0).Inputs);
			Assert.Equal(2, blockInfo.Transactions.ElementAt(0).Outputs.Count());
			Assert.Single(blockInfo.Transactions.ElementAt(1).Inputs);
			Assert.Equal(2, blockInfo.Transactions.ElementAt(1).Outputs.Count());

			Assert.Equal("01660101", blockInfo.Transactions.ElementAt(0).Inputs.ElementAt(0).Coinbase);
			Assert.Equal(RpcPubkeyType.TxPubkeyhash, blockInfo.Transactions.ElementAt(0).Outputs.ElementAt(0).PubkeyType);
			Assert.Equal(RpcPubkeyType.TxNullData, blockInfo.Transactions.ElementAt(0).Outputs.ElementAt(1).PubkeyType);

			Assert.Equal(Money.Coins(50), blockInfo.Transactions.ElementAt(1).Inputs.ElementAt(0).PrevOutput.Value);
			Assert.Equal(Money.Coins((decimal)48.99995500), blockInfo.Transactions.ElementAt(1).Outputs.ElementAt(0).Value);
			Assert.Equal(Money.Coins((decimal)1.00000000), blockInfo.Transactions.ElementAt(1).Outputs.ElementAt(1).Value);
			Assert.Equal(RpcPubkeyType.TxPubkeyhash, blockInfo.Transactions.ElementAt(1).Inputs.ElementAt(0).PrevOutput.PubkeyType);
			Assert.Equal(RpcPubkeyType.TxPubkeyhash, blockInfo.Transactions.ElementAt(1).Outputs.ElementAt(0).PubkeyType);
			Assert.Equal(RpcPubkeyType.TxPubkeyhash, blockInfo.Transactions.ElementAt(1).Outputs.ElementAt(1).PubkeyType);
		}
	}
}
