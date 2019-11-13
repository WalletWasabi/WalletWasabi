using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	public class TrustedNodeNotifyingBehavior : NodeBehavior
	{
		public RPCClient RpcClient { get; }

		public event EventHandler<uint256> TransactionInv;

		public event EventHandler<uint256> BlockInv;

		public event EventHandler<SmartTransaction> Transaction;

		public event EventHandler<Block> Block;

		public event EventHandler<IEnumerable<BlockHeader>> Reorg;

		private static List<BlockHeader> ProcessedBlocks { get; } = new List<BlockHeader>();
		private static HashSet<uint256> ArrivedInvs { get; } = new HashSet<uint256>();
		private static AsyncLock ProcessedBlocksLock { get; } = new AsyncLock();
		private static object ArrivedInvsLock { get; } = new object();

		public TrustedNodeNotifyingBehavior(RPCClient rpcClient)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		protected override void AttachCore()
		{
			AttachedNode.MessageReceived += AttachedNode_MessageReceivedAsync;
		}

		protected override void DetachCore()
		{
			AttachedNode.MessageReceived -= AttachedNode_MessageReceivedAsync;
		}

		private async void AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
		{
			try
			{
				if (message.Message.Payload is TxPayload txPayload)
				{
					Transaction?.Invoke(this, new SmartTransaction(txPayload.Object, Height.Mempool));
				}
				else if (message.Message.Payload is BlockPayload blockPayload)
				{
					Block arrivedBlock = blockPayload.Object;
					var arrivedHeader = arrivedBlock.Header;
					arrivedHeader.PrecomputeHash(false, true);

					using (await ProcessedBlocksLock.LockAsync().ConfigureAwait(false))
					{
						if (!ProcessedBlocks.Any())
						{
							AddBlock(arrivedBlock);
							return;
						}

						// If block was already processed return.
						if (ProcessedBlocks.Any(x => x.GetHash() == arrivedHeader.GetHash()))
						{
							return;
						}

						// If this block follows the proper order then add.
						if (ProcessedBlocks.Last().GetHash() == arrivedHeader.HashPrevBlock)
						{
							AddBlock(arrivedBlock);
							return;
						}

						// Else let's sort out things.
						var foundPrevBlock = ProcessedBlocks.FirstOrDefault(x => x.GetHash() == arrivedHeader.HashPrevBlock);
						// Missed notifications on some previous blocks.
						if (foundPrevBlock != null)
						{
							// Reorg happened.
							ReorgToBlock(foundPrevBlock);
							AddBlock(arrivedBlock);
							return;
						}

						var missedBlocks = new List<Block>
						{
							arrivedBlock
						};
						var currentHeader = arrivedHeader;
						while (true)
						{
							Block missedBlock = null;
							try
							{
								missedBlock = await RpcClient.GetBlockAsync(currentHeader.HashPrevBlock).ConfigureAwait(false);
							}
							catch (RPCException ex) when (ex.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY)
							{
								return;
							}

							if (missedBlock is null)
							{
								return;
							}

							currentHeader = missedBlock.Header;
							currentHeader.PrecomputeHash(false, true);
							missedBlocks.Add(missedBlock);

							if (missedBlocks.Count > 100)
							{
								var processedBlocksClone = ProcessedBlocks.ToList();
								ProcessedBlocks.Clear();
								Reorg?.Invoke(this, processedBlocksClone);
								Logger.LogCritical("A reorg detected over 100 blocks. Wasabi cannot handle that.");
								break;
							}

							// If we found the proper chain.
							foundPrevBlock = ProcessedBlocks.FirstOrDefault(x => x.GetHash() == currentHeader.HashPrevBlock);
							if (foundPrevBlock != null)
							{
								// If the last block hash is not what we found, then we missed a reorg also.
								if (foundPrevBlock.GetHash() != ProcessedBlocks.Last().GetHash())
								{
									ReorgToBlock(foundPrevBlock);
								}

								break;
							}
						}

						missedBlocks.Reverse();
						foreach (var b in missedBlocks)
						{
							lock (ArrivedInvsLock)
							{
								if (ArrivedInvs.Contains(b.GetHash()))
								{
									AddBlock(b);
									continue;
								}
								ArrivedInvs.Add(b.GetHash());
							}
							BlockInv?.Invoke(this, b.GetHash());
							AddBlock(b);
						}
					}
				}
				else if (message.Message.Payload is InvPayload invPayload)
				{
					var getDataPayload = new GetDataPayload();
					foreach (var inv in invPayload.Inventory)
					{
						if (inv.Type.HasFlag(InventoryType.MSG_TX))
						{
							TransactionInv?.Invoke(this, inv.Hash);
							getDataPayload.Inventory.Add(inv);
						}

						if (inv.Type.HasFlag(InventoryType.MSG_BLOCK))
						{
							lock (ArrivedInvsLock)
							{
								if (ArrivedInvs.Contains(inv.Hash))
								{
									continue;
								}
								ArrivedInvs.Add(inv.Hash);
							}
							BlockInv?.Invoke(this, inv.Hash);
							getDataPayload.Inventory.Add(inv);
						}
					}

					if (getDataPayload.Inventory.Any() && node.IsConnected)
					{
						// ask for the whole transaction
						await node.SendMessageAsync(getDataPayload).ConfigureAwait(false);
					}
				}
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogDebug(ex);
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Ignoring {ex.GetType()}: {ex.Message}");
				Logger.LogDebug(ex);
			}
		}

		private void ReorgToBlock(BlockHeader correctBlock)
		{
			var index = ProcessedBlocks.IndexOf(correctBlock);
			int countToRemove = ProcessedBlocks.Count - (index + 1);
			var toRemove = ProcessedBlocks.TakeLast(countToRemove).ToList();
			ProcessedBlocks.RemoveRange(index + 1, countToRemove);
			Reorg?.Invoke(this, toRemove);
		}

		private void AddBlock(Block block)
		{
			ProcessedBlocks.Add(block.Header);
			Block?.Invoke(this, block);
		}

		public override object Clone()
		{
			// Note that, this is not clone! So this must be used after we are connected to a node, because it'll have as many clones as nodes.
			return new TrustedNodeNotifyingBehavior(RpcClient);
		}
	}
}
