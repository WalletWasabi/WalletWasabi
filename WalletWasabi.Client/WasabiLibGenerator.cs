using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Client;

public static class WasabiLibGenerator
{
	public static HashSet<(Type Type, string PropertyName)> AllowedAccessors { get; } = new();

	public static string Generate()
	{
		AllowedAccessors.Clear();
		var accessorLines = new List<string>();

		// Simple accessor: registers (T, PropertyName) and generates (define-accessor name property)
		void DefineAccessor<T, TProp>(string accessorName, Expression<Func<T, TProp>> expr)
		{
			if (expr.Body is MemberExpression memberExpr)
			{
				var propertyName = memberExpr.Member.Name;
				AllowedAccessors.Add((typeof(T), propertyName));
				accessorLines.Add($"(define-accessor {accessorName} {propertyName.ToLowerInvariant()})");
			}
		}

		// Chained accessor: registers (T, PropertyName) and generates (define-accessor name property getter)
		void DefineAccessorWithGetter<T, TProp>(string accessorName, Expression<Func<T, TProp>> expr, string getterName)
		{
			if (expr.Body is MemberExpression memberExpr)
			{
				var propertyName = memberExpr.Member.Name;
				AllowedAccessors.Add((typeof(T), propertyName));
				accessorLines.Add($"(define-accessor {accessorName} {propertyName.ToLowerInvariant()} {getterName})");
			}
		}

		// ==========================================
		// HdPubKey Accessors
		// ==========================================
		DefineAccessor("hdpubkey-pubkey", (HdPubKey hd) => hd.PubKey);
		DefineAccessor("hdpubkey-keypath", (HdPubKey hd) => hd.FullKeyPath);
		DefineAccessor("hdpubkey-labels", (HdPubKey hd) => hd.Labels);
		DefineAccessor("hdpubkey-state", (HdPubKey hd) => hd.KeyState);
		DefineAccessor("hdpubkey-index", (HdPubKey hd) => hd.Index);
		DefineAccessor("hdpubkey-internal?", (HdPubKey hd) => hd.IsInternal);

		// Additional HdPubKey accessors
		DefineAccessor("hdpubkey-p2taproot", (HdPubKey hd) => hd.P2Taproot);
		DefineAccessor("hdpubkey-p2wpkh-script", (HdPubKey hd) => hd.P2wpkhScript);
		DefineAccessor("hdpubkey-cluster", (HdPubKey hd) => hd.Cluster);
		DefineAccessor("keypath-indexes", (KeyPath kp) => kp.Indexes);

		// ==========================================
		// Wallet Accessors
		// ==========================================
		DefineAccessor("wallet-name", (Wallet w) => w.WalletName);
		DefineAccessor("wallet-keymanager", (Wallet w) => w.KeyManager);
		DefineAccessor("wallet-loaded?", (Wallet w) => w.Loaded);

		// KeyManager accessors (chained via wallet-keymanager)
		DefineAccessorWithGetter("wallet-path", (KeyManager km) => km.FilePath, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-watch-only?", (KeyManager km) => km.IsWatchOnly, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-hardware-wallet?", (KeyManager km) => km.IsHardwareWallet, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-auto-coinjoin?", (KeyManager km) => km.AutoCoinJoin, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-non-private-coin-isolation?", (KeyManager km) => km.NonPrivateCoinIsolation, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-anonscore-target", (KeyManager km) => km.AnonScoreTarget, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-excluded-coins-from-coinjoin", (KeyManager km) => km.ExcludedCoinsFromCoinJoin, "wallet-keymanager");

		// Additional KeyManager accessors (chained via wallet-keymanager)
		DefineAccessorWithGetter("wallet-master-fingerprint", (KeyManager km) => km.MasterFingerprint, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-segwit-extpubkey", (KeyManager km) => km.SegwitExtPubKey, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-taproot-extpubkey", (KeyManager km) => km.TaprootExtPubKey, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-segwit-account-keypath", (KeyManager km) => km.SegwitAccountKeyPath, "wallet-keymanager");
		DefineAccessorWithGetter("wallet-taproot-account-keypath", (KeyManager km) => km.TaprootAccountKeyPath, "wallet-keymanager");

		// ==========================================
		// Transaction Accessors (SmartTransaction)
		// ==========================================
		DefineAccessor("transaction-wallet-inputs", (SmartTransaction tx) => tx.WalletInputs);
		DefineAccessor("transaction-wallet-outputs", (SmartTransaction tx) => tx.WalletOutputs);
		DefineAccessor("transaction-foreign-inputs", (SmartTransaction tx) => tx.ForeignInputs);
		DefineAccessor("transaction-foreign-outputs", (SmartTransaction tx) => tx.ForeignOutputs);
		DefineAccessor("transaction-raw", (SmartTransaction tx) => tx.Transaction);
		DefineAccessor("transaction-block-index", (SmartTransaction tx) => tx.BlockIndex);
		DefineAccessor("transaction-labels", (SmartTransaction tx) => tx.Labels);
		DefineAccessor("transaction-first-seen", (SmartTransaction tx) => tx.FirstSeen);
		DefineAccessor("transaction-speedup?", (SmartTransaction tx) => tx.IsSpeedup);
		DefineAccessor("transaction-cancellation?", (SmartTransaction tx) => tx.IsCancellation);
		DefineAccessor("transaction-cpfp?", (SmartTransaction tx) => tx.IsCPFP);
		DefineAccessor("transaction-confirmed?", (SmartTransaction tx) => tx.Confirmed);
		DefineAccessor("transaction-replacement?", (SmartTransaction tx) => tx.IsReplacement);
		DefineAccessor("transaction-coinjoin?", (SmartTransaction tx) => tx.IsWasabi2Cj);
		DefineAccessor("transaction-raw-height", (SmartTransaction tx) => tx.Height);
		DefineAccessor("transaction-raw-blockhash", (SmartTransaction tx) => tx.BlockHash);

		// ==========================================
		// OutPoint Accessors
		// ==========================================
		DefineAccessor("outpoint-hash", (OutPoint op) => op.Hash);
		DefineAccessor("outpoint-n", (OutPoint op) => op.N);

		// ==========================================
		// Coin Accessors
		// ==========================================
		DefineAccessor("coin-tx", (SmartCoin c) => c.Transaction);
		DefineAccessor("coin-outpoint", (SmartCoin c) => c.Outpoint);
		DefineAccessor("coin-anonymityset", (SmartCoin c) => c.AnonymitySet);
		DefineAccessor("coin-spent-by", (SmartCoin c) => c.SpenderTransaction);
		DefineAccessor("coin-confirmed?", (SmartCoin c) => c.Confirmed);
		DefineAccessor("coin-banned?", (SmartCoin c) => c.IsBanned);
		DefineAccessor("coin-banned-until", (SmartCoin c) => c.BannedUntilUtc);
		DefineAccessor("coin-script-pubkey", (SmartCoin c) => c.ScriptPubKey);
		DefineAccessor("coin-script-pubkey-type", (SmartCoin c) => c.ScriptType);
		DefineAccessor("coin-excluded-from-coinjoin?", (SmartCoin c) => c.IsExcludedFromCoinJoin);
		DefineAccessor("coin-pubkey", (SmartCoin c) => c.HdPubKey);

		// Additional SmartCoin accessors
		DefineAccessor("coin-raw-amount", (SmartCoin c) => c.Amount);
		DefineAccessor("coin-raw-height", (SmartCoin c) => c.Height);

		// Money accessors
		DefineAccessor("money-satoshi", (Money m) => m.Satoshi);

		// Cluster accessors (chained via coin-pubkey -> hdpubkey-cluster)
		DefineAccessorWithGetter("cluster-labels", (Cluster c) => c.Labels, "hdpubkey-cluster");

		// ==========================================
		// Global Accessors
		// ==========================================
		DefineAccessor("get-network", (Global g) => g.Network);
		DefineAccessor("filterheaders", (Global g) => g.FilterHeaders);

		// FilterHeaderChain accessors (direct, used with header-chain variable)
		DefineAccessor("filterheaders-server-tip-height", (FilterHeaderChain fh) => fh.ServerTipHeight);
		DefineAccessor("filterheaders-tip-height", (FilterHeaderChain fh) => fh.TipHeight);
		DefineAccessor("filterheaders-tip-hash", (FilterHeaderChain fh) => fh.TipHash);
		DefineAccessor("filterheaders-hash-count", (FilterHeaderChain fh) => fh.HashCount);
		DefineAccessor("filterheaders-hashes-left", (FilterHeaderChain fh) => fh.HashesLeft);

		// Build the accessors string
		var accessors = string.Join("\n", accessorLines);

		return $"""
		       (load "Stdlib.scm")

		       ;;; ----------------------
		       ;;; Accessor Macro
		       ;;; ----------------------

		       ;; Reduces boilerplate for property accessors
		       ;; Usage: (define-accessor name property)           -> (define (name obj) (__get 'property obj))
		       ;;        (define-accessor name property getter)    -> (define (name obj) (__get 'property (getter obj)))
		       (define-syntax define-accessor
		         (syntax-rules ()
		           ((_ name property getter)
		            (define (name obj) (__get 'property (getter obj))))
		           ((_ name property)
		            (define (name obj) (__get 'property obj)))))

		       ;;; ----------------------
		       ;;; Helpers
		       ;;; ----------------------

		       (define (bitcoin->satoshi n) (* n 100000000))
		       (define (satoshi->bitcoin n) (/ n 100000000.0))

		       (define (height native-height)
		         (let ((hi (string->number (native->string native-height))))
		           (or hi (native->string native-height))))

		       (define (get-wallet-by-name name)
		         (find (lambda (w) (string=? name (wallet-name w))) (wallets)))

		       (define (get-opened-wallets)
		         (filter wallet-loaded? (wallets)))

		       ;; Sum amounts from a list of coins
		       (define (sum-amounts coins)
		         (foldl + 0 (map coin-amount coins)))

		       ;; Group elements by a key function
		       ;; Returns association list: ((key1 (items...)) (key2 (items...)) ...)
		       (define (group-by key-fn lst)
		         (foldl (lambda (item acc)
		                  (let* ((k (key-fn item))
		                         (existing (assoc k acc)))
		                    (if existing
		                        (map (lambda (pair)
		                               (if (equal? (car pair) k)
		                                   (list k (cons item (cadr pair)))
		                                   pair))
		                             acc)
		                        (cons (list k (list item)) acc))))
		                '() lst))

		       ;;; ----------------------
		       ;;; HdPubKey Accessors
		       ;;; ----------------------

		       {accessors}

		       (define (hdpubkey-external? key)
		         (not (hdpubkey-internal? key)))

		       (define (hdpubkey-used? key)
		         (string=? "Used" (native->string (hdpubkey-state key))))

		       (define (hdpubkey-clean? key)
		         (string=? "Clean" (native->string (hdpubkey-state key))))

		       (define (hdpubkey-locked? key)
		         (string=? "Locked" (native->string (hdpubkey-state key))))

		       ;; Determine script type from keypath purpose (84' = SegWit, 86' = Taproot)
		       (define (hdpubkey-script key)
		         (let* ((indexes (keypath-indexes (hdpubkey-keypath key)))
		                (purpose (car indexes)))
		           (if (= purpose 86)
		               (hdpubkey-p2taproot key)
		               (hdpubkey-p2wpkh-script key))))

		       (define (hdpubkey-address key)
		         (native->string (script->address (hdpubkey-script key))))

		       (define (hdpubkey-info key)
		         `(("address"  ,(hdpubkey-address key))
		           ("keypath"  ,(native->string (hdpubkey-keypath key)))
		           ("labels"   ,(hdpubkey-labels key))
		           ("state"    ,(native->string (hdpubkey-state key)))
		           ("index"    ,(hdpubkey-index key))
		           ("internal" ,(hdpubkey-internal? key))))

		       ;;; ----------------------
		       ;;; Wallet Functions
		       ;;; ----------------------

		       (define (wallet-master-key-fingerprint wallet)
		         (native->string (wallet-master-fingerprint wallet)))

		       (define (wallet-unspent-coins wallet)
		         (filter coin-unspent? (wallet-coins wallet)))

		       (define (wallet-balance wallet)
		         (foldl + 0 (map coin-amount (wallet-unspent-coins wallet))))

		       (define (wallet-info wallet)
		         `(("name"     ,(wallet-name wallet))
		           ("loaded"   ,(wallet-loaded? wallet))
		           ("readOnly" ,(wallet-watch-only? wallet))
		           ("path"     ,(wallet-path wallet))))

		       ;;; ----------------------
		       ;;; Wallet Address Functions
		       ;;; ----------------------

		       ;; All HD public keys for wallet
		       (define (wallet-keys wallet)
		         (wallet-hdpubkeys wallet))

		       ;; Filter external (receive) keys
		       (define (wallet-external-keys wallet)
		         (filter hdpubkey-external? (wallet-keys wallet)))

		       ;; Filter internal (change) keys
		       (define (wallet-internal-keys wallet)
		         (filter hdpubkey-internal? (wallet-keys wallet)))

		       ;; Filter used keys
		       (define (wallet-used-keys wallet)
		         (filter hdpubkey-used? (wallet-keys wallet)))

		       ;; Filter unused (clean) keys
		       (define (wallet-unused-keys wallet)
		         (filter hdpubkey-clean? (wallet-keys wallet)))

		       ;; Get first unused external address (next receive address)
		       (define (wallet-receive-address wallet)
		         (let ((unused-external (filter (lambda (k)
		                                          (and (hdpubkey-external? k)
		                                               (hdpubkey-clean? k)))
		                                        (wallet-keys wallet))))
		           (if (null? unused-external)
		               #f
		               (hdpubkey-address (car unused-external)))))

		       ;; Get all addresses as strings
		       (define (wallet-addresses wallet)
		         (map hdpubkey-address (wallet-keys wallet)))

		       ;; Get all used addresses
		       (define (wallet-used-addresses wallet)
		         (map hdpubkey-address (wallet-used-keys wallet)))

		       ;; Get all unused addresses
		       (define (wallet-unused-addresses wallet)
		         (map hdpubkey-address (wallet-unused-keys wallet)))

		       ;; Address count statistics
		       (define (wallet-address-stats wallet)
		         (let ((keys (wallet-keys wallet)))
		           `(("total"         ,(length keys))
		             ("external"      ,(length (filter hdpubkey-external? keys)))
		             ("internal"      ,(length (filter hdpubkey-internal? keys)))
		             ("used"          ,(length (filter hdpubkey-used? keys)))
		             ("unused"        ,(length (filter hdpubkey-clean? keys)))
		             ("locked"        ,(length (filter hdpubkey-locked? keys))))))

		       ;;; ----------------------
		       ;;; Transaction Functions
		       ;;; ----------------------

		       (define (transaction-hash tx)
		         (native->string (__get 'gethash tx)))

		       (define (transaction-block-hash tx)
		         (native->string (transaction-raw-blockhash tx)))

		       (define (transaction-height tx)
		         (height (transaction-raw-height tx)))

		       (define (transaction-info tx)
		         `(("hash"           ,(transaction-hash tx))
		           ("height"         ,(transaction-height tx))
		           ("blockHash"      ,(transaction-block-hash tx))
		           ("isConfirmed"    ,(transaction-confirmed? tx))
		           ("isReplacement"  ,(transaction-replacement? tx))
		           ("isCancellation" ,(transaction-cancellation? tx))
		           ("isCoinjoin"     ,(transaction-coinjoin? tx))
		           ("isCpfp"         ,(transaction-cpfp? tx))
		           ("isSpeedup"      ,(transaction-speedup? tx))
		           ("firstSeen"      ,(transaction-first-seen tx))))

		       ;;; ----------------------
		       ;;; Coin Functions
		       ;;; ----------------------

		       (define (coin-amount coin)
		         (satoshi->bitcoin (money-satoshi (coin-raw-amount coin))))

		       (define (coin-height coin)
		         (height (coin-raw-height coin)))

		       ;; Check if coin is spent (has a spender transaction)
		       (define (coin-spent? coin)
		         (not (null? (coin-spent-by coin))))

		       (define (coin-unspent? coin)
		         (not (coin-spent? coin)))

		       (define (coin-cluster coin)
		         (hdpubkey-cluster (coin-pubkey coin)))

		       (define (coin-labels coin)
		         (cluster-labels (coin-pubkey coin)))

		       (define (coin-keypath coin)
		         (hdpubkey-keypath (coin-pubkey coin)))

		       (define (coin-address coin)
		         (native->string (script->address (coin-script-pubkey coin))))

		       (define (coin-info coin)
		         `(("outpoint"       ,(native->string (coin-outpoint coin)))
		           ("amount"         ,(coin-amount coin))
		           ("labels"         ,(coin-labels coin))
		           ("anonymityScore" ,(coin-anonymityset coin))
		           ("confirmed"      ,(coin-confirmed? coin))
		           ("spent"          ,(coin-spent? coin))
		           ("keypath"        ,(native->string (coin-keypath coin)))
		           ("address"        ,(coin-address coin))))

		       ;;; ----------------------
		       ;;; Coin Filtering & Analysis
		       ;;; ----------------------

		       ;; Number of confirmations for a coin
		       (define (coin-confirmations coin)
		         (let ((h (coin-height coin)))
		           (if (number? h)
		               (- (remote-tip-height) h)
		               0)))

		       ;; Check if coin meets wallet's anonymity target
		       (define (coin-private? coin wallet)
		         (>= (coin-anonymityset coin) (wallet-anonscore-target wallet)))

		       ;; Filter coins by anonymity score threshold
		       (define (wallet-private-coins wallet threshold)
		         (filter (lambda (c) (>= (coin-anonymityset c) threshold))
		                 (wallet-unspent-coins wallet)))

		       (define (wallet-non-private-coins wallet threshold)
		         (filter (lambda (c) (< (coin-anonymityset c) threshold))
		                 (wallet-unspent-coins wallet)))

		       ;; Group coins by their labels
		       (define (coins-by-label coins)
		         (group-by coin-labels coins))

		       ;; Group coins by address
		       (define (coins-by-address coins)
		         (group-by coin-address coins))

		       ;;; ----------------------
		       ;;; Balance Breakdowns
		       ;;; ----------------------

		       (define (wallet-confirmed-balance wallet)
		         (sum-amounts (filter coin-confirmed? (wallet-unspent-coins wallet))))

		       (define (wallet-unconfirmed-balance wallet)
		         (sum-amounts (filter (lambda (c) (not (coin-confirmed? c)))
		                              (wallet-unspent-coins wallet))))

		       (define (wallet-private-balance wallet)
		         (sum-amounts (wallet-private-coins wallet (wallet-anonscore-target wallet))))

		       (define (wallet-non-private-balance wallet)
		         (sum-amounts (wallet-non-private-coins wallet (wallet-anonscore-target wallet))))

		       ;;; ----------------------
		       ;;; Transaction Helpers
		       ;;; ----------------------

		       ;; Number of confirmations for a transaction
		       (define (transaction-confirmations tx)
		         (let ((h (transaction-height tx)))
		           (if (number? h)
		               (- (remote-tip-height) h)
		               0)))

		       ;; Filter transactions by status
		       (define (wallet-pending-transactions wallet)
		         (filter (lambda (tx) (not (transaction-confirmed? tx)))
		                 (wallet-transactions wallet)))

		       (define (wallet-confirmed-transactions wallet)
		         (filter transaction-confirmed? (wallet-transactions wallet)))

		       (define (wallet-coinjoin-transactions wallet)
		         (filter transaction-coinjoin? (wallet-transactions wallet)))

		       ;;; ----------------------
		       ;;; Wallet Statistics
		       ;;; ----------------------

		       ;; Number of unspent coins (UTXOs)
		       (define (wallet-utxo-count wallet)
		         (length (wallet-unspent-coins wallet)))

		       ;; Average anonymity score across all unspent coins
		       (define (wallet-avg-anonscore wallet)
		         (let ((coins (wallet-unspent-coins wallet)))
		           (if (null? coins)
		               0
		               (/ (foldl + 0 (map coin-anonymityset coins))
		                  (length coins)))))

		       ;; Minimum anonymity score among unspent coins
		       (define (wallet-min-anonscore wallet)
		         (let ((coins (wallet-unspent-coins wallet)))
		           (if (null? coins)
		               0
		               (foldl min (coin-anonymityset (car coins))
		                      (map coin-anonymityset (cdr coins))))))

		       ;; Count of private vs non-private coins
		       (define (wallet-privacy-summary wallet)
		         (let* ((threshold (wallet-anonscore-target wallet))
		                (coins (wallet-unspent-coins wallet))
		                (private (filter (lambda (c) (>= (coin-anonymityset c) threshold)) coins))
		                (non-private (filter (lambda (c) (< (coin-anonymityset c) threshold)) coins))
		                (private-bal (sum-amounts private))
		                (non-private-bal (sum-amounts non-private)))
		           `(("privateCount"      ,(length private))
		             ("nonPrivateCount"   ,(length non-private))
		             ("privateBalance"    ,private-bal)
		             ("privateBalanceUsd" ,(btc->usd private-bal))
		             ("nonPrivateBalance" ,non-private-bal)
		             ("nonPrivateBalanceUsd" ,(btc->usd non-private-bal))
		             ("avgAnonScore"      ,(wallet-avg-anonscore wallet))
		             ("minAnonScore"      ,(wallet-min-anonscore wallet))
		             ("targetAnonScore"   ,threshold))))

		       ;; Check if a coin is economically spendable at given fee rate
		       ;; Assumes ~68 vbytes input cost for P2WPKH
		       (define (coin-spendable? coin fee-rate-sat-vb)
		         (let ((input-cost (satoshi->bitcoin (* 68 fee-rate-sat-vb))))
		           (> (coin-amount coin) input-cost)))

		       ;; Find dust coins (not economically spendable at economy fee rate)
		       (define (wallet-dust-coins wallet)
		         (let ((rate (or (fee-rate-economy) 1)))
		           (filter (lambda (c) (not (coin-spendable? c rate)))
		                   (wallet-unspent-coins wallet))))

		       ;; UTXO health summary
		       (define (wallet-utxo-health wallet)
		         (let* ((coins (wallet-unspent-coins wallet))
		                (rate (or (fee-rate-economy) 1))
		                (dust (filter (lambda (c) (not (coin-spendable? c rate))) coins))
		                (spendable (filter (lambda (c) (coin-spendable? c rate)) coins)))
		           `(("totalUtxos"      ,(length coins))
		             ("spendableUtxos"  ,(length spendable))
		             ("dustUtxos"       ,(length dust))
		             ("dustBalance"     ,(sum-amounts dust))
		             ("spendableBalance" ,(sum-amounts spendable))
		             ("economyFeeRate"  ,rate))))

		       ;;; ----------------------
		       ;;; Global State
		       ;;; ----------------------

		       (define network      (get-network (global)))
		       (define header-chain (filterheaders (global)))

		       (define (remote-tip-height) (filterheaders-server-tip-height header-chain))
		       (define (local-tip-height)  (filterheaders-tip-height header-chain))
		       (define (local-tip-hash)    (filterheaders-tip-hash header-chain))
		       (define (headers-count)     (filterheaders-hash-count header-chain))
		       (define (headers-left)      (filterheaders-hashes-left header-chain))

		       ;;; ----------------------
		       ;;; Fee Rate Functions
		       ;;; ----------------------

		       ;; Get all fee rate estimations as association list
		       ;; Each entry is (blocks . sat/vB)
		       (define (fee-rates)
		         (let ((estimations (fee-rate-estimations)))
		           (if (null? estimations)
		               '()
		               (map (lambda (kvp)
		                      (list (__get 'key kvp)
		                            (__get 'satoshiperbyte (__get 'value kvp))))
		                    estimations))))

		       ;; Get fee rate for specific confirmation target (in sat/vB)
		       ;; Uses numeric comparison since dictionary keys are converted to RealNumber
		       (define (fee-rate-for-target blocks)
		         (let ((rates (fee-rates)))
		           (if (null? rates)
		               #f
		               (let ((found (find (lambda (pair) (= (car pair) blocks)) rates)))
		                 (if found
		                     (cadr found)
		                     #f)))))

		       ;; Common fee rate shortcuts
		       (define (fee-rate-fast)     (fee-rate-for-target 2))
		       (define (fee-rate-normal)   (fee-rate-for-target 6))
		       (define (fee-rate-economy)  (fee-rate-for-target 36))
		       (define (fee-rate-minimum)  (fee-rate-for-target 1008))

		       ;; Estimate fee for a transaction given vsize and target
		       (define (estimate-fee vsize target-blocks)
		         (let ((rate (fee-rate-for-target target-blocks)))
		           (if rate
		               (satoshi->bitcoin (* vsize rate))
		               #f)))

		       ;;; ----------------------
		       ;;; Exchange Rate Functions
		       ;;; ----------------------

		       ;; Convert BTC to USD
		       (define (btc->usd btc)
		         (let ((rate (exchange-rate-usd)))
		           (* btc rate)))

		       ;; Convert satoshi to USD
		       (define (satoshi->usd sats)
		         (btc->usd (satoshi->bitcoin sats)))

		       ;; Convert USD to BTC
		       (define (usd->btc usd)
		         (let ((rate (exchange-rate-usd)))
		           (if (zero? rate)
		               0
		               (/ usd rate))))

		       ;; Coin value in USD
		       (define (coin-value-usd coin)
		         (btc->usd (coin-amount coin)))

		       ;; Wallet balance in USD
		       (define (wallet-balance-usd wallet)
		         (btc->usd (wallet-balance wallet)))

		       ;;; ----------------------
		       ;;; Tor & Network Status
		       ;;; ----------------------

		       (define (tor-mode)
		         (__get 'tormode (tor-settings)))

		       (define (tor-info)
		         `(("running"   ,(tor-running?))
		           ("mode"      ,(native->string (tor-mode)))
		           ("onion"     ,(onion-service-uri))))

		       (define (network-info)
		         (let ((local (__get 'height (local-tip-height)))
		               (remote (__get 'height (remote-tip-height))))
		           `(("network"      ,(native->string network))
		             ("torRunning"   ,(tor-running?))
		             ("localHeight"  ,local)
		             ("remoteHeight" ,remote)
		             ("synced"       ,(eq? remote local))
		             ("headersLeft"  ,(headers-left)))))

		       ;;; ----------------------
		       ;;; Connected Nodes (P2P Peers)
		       ;;; ----------------------

		       ;; Get node endpoint as string
		       (define (node-endpoint node)
		         (native->string (__get 'endpoint (__get 'peer node))))

		       ;; Get node user agent
		       (define (node-user-agent node)
		         (let ((version (__get 'peerversion node)))
		           (if version
		               (__get 'useragent version)
		               "")))

		       ;; Get node protocol version
		       (define (node-protocol-version node)
		         (let ((version (__get 'peerversion node)))
		           (if version
		               (__get 'version version)
		               0)))

		       ;; Get node services flags
		       (define (node-services node)
		         (let ((version (__get 'peerversion node)))
		           (if version
		               (__get 'services version)
		               0)))

		       ;; Get node start height (blockchain height at connection time)
		       (define (node-start-height node)
		         (let ((version (__get 'peerversion node)))
		           (if version
		               (__get 'startheight version)
		               0)))

		       ;; Check if node is connected
		       (define (node-connected? node)
		         (__get 'isconnected node))

		       ;; NODE_COMPACT_FILTERS = 64 (1 << 6) per BIP157
		       (define NODE_COMPACT_FILTERS 64)

		       ;; Check if a flag bit is set using arithmetic
		       ;; (has-flag? 65 64) => #t (bit 6 set), (has-flag? 1 64) => #f
		       (define (has-flag? value flag)
		         (= 1 (modulo (floor (/ value flag)) 2)))

		       ;; Check if node supports compact filters (BIP157/158)
		       (define (node-supports-filters? node)
		         (let ((services (node-services node)))
		           (and (number? services)
		                (has-flag? services NODE_COMPACT_FILTERS))))

		       ;; Get node info as association list
		       (define (node-info node)
		         `(("endpoint"        ,(node-endpoint node))
		           ("userAgent"       ,(node-user-agent node))
		           ("protocolVersion" ,(node-protocol-version node))
		           ("startHeight"     ,(node-start-height node))
		           ("connected"       ,(node-connected? node))
		           ("compactFilters"  ,(node-supports-filters? node))))

		       ;; Get all connected node endpoints
		       (define (peer-endpoints)
		         (map node-endpoint (connected-nodes)))

		       ;; Get count of connected nodes
		       (define (peer-count)
		         (length (connected-nodes)))

		       ;; Get detailed info for all connected peers
		       (define (peers-info)
		         (map node-info (connected-nodes)))

		       ;; Filter nodes that support compact filters
		       (define (filter-nodes)
		         (filter node-supports-filters? (connected-nodes)))

		       ;; Filter nodes that do NOT support compact filters
		       (define (non-filter-nodes)
		         (filter (lambda (n) (not (node-supports-filters? n))) (connected-nodes)))

		       ;; Endpoints of nodes supporting compact filters
		       (define (filter-node-endpoints)
		         (map node-endpoint (filter-nodes)))

		       ;; Endpoints of nodes NOT supporting compact filters
		       (define (non-filter-node-endpoints)
		         (map node-endpoint (non-filter-nodes)))

		       ;; Peer statistics by capability
		       (define (peer-stats)
		         (let* ((all (connected-nodes))
		                (with-filters (filter node-supports-filters? all))
		                (without-filters (filter (lambda (n) (not (node-supports-filters? n))) all)))
		           `(("total"              ,(length all))
		             ("withCompactFilters" ,(length with-filters))
		             ("withoutCompactFilters" ,(length without-filters))
		             ("filterEndpoints"    ,(map node-endpoint with-filters))
		             ("otherEndpoints"     ,(map node-endpoint without-filters)))))

		       ;;; ----------------------
		       ;;; Sync Info
		       ;;; ----------------------

		       (define (sync-info)
		         (let* ((local (__get 'height (local-tip-height)))
		                (remote (__get 'height (remote-tip-height)))
		                (left (headers-left))
		                (synced? (eq? remote local))
		                (all-nodes (connected-nodes))
		                (filter-nodes-list (filter node-supports-filters? all-nodes))
		                (other-nodes-list (filter (lambda (n) (not (node-supports-filters? n))) all-nodes))
		                (rates (fee-rates))
		                (fast-rate (fee-rate-for-target 2))
		                (normal-rate (fee-rate-for-target 6))
		                (economy-rate (fee-rate-for-target 36)))
		           `(("network"        ,(native->string network))
		             ("synchronized"   ,synced?)
		             ("localHeight"    ,local)
		             ("remoteHeight"   ,remote)
		             ("headersLeft"    ,left)
		             ("peers"          (("total"         ,(length all-nodes))
		                                ("filterNodes"   ,(length filter-nodes-list))
		                                ("otherNodes"    ,(length other-nodes-list))))
		             ("filterPeers"    ,(map node-endpoint filter-nodes-list))
		             ("otherPeers"     ,(map node-endpoint other-nodes-list))
		             ("tor"            (("running"       ,(tor-running?))
		                                ("mode"          ,(native->string (tor-mode)))
		                                ("onion"         ,(onion-service-uri))))
		             ("feeRates"       (("fast"          ,fast-rate)
		                                ("normal"        ,normal-rate)
		                                ("economy"       ,economy-rate)))
		             ("exchangeRate"   ,(exchange-rate-usd)))))

		       ;;; ----------------------
		       ;;; RPC Equivalent Functions
		       ;;; ----------------------

		       (define (coin->rpc_info coin)
		         `(("outpoint"       ,(native->string (coin-outpoint coin)))
		           ("amount"         ,(bitcoin->satoshi (coin-amount coin)))
		           ("anonymityScore" ,(coin-anonymityset coin))
		           ("confirmed"      ,(coin-confirmed? coin))
		           ("confirmations"  ,(- (remote-tip-height) (coin-height coin)))
		           ("keypath"        ,(native->string (coin-keypath coin)))
		           ("address"        ,(coin-address coin))))

		       (define (unspent-coins wallet)
		         (map (lambda (coin)
		                (append (coin->rpc_info coin)
		                        `(("labels"             ,(string-join ", " (coin-labels coin)))
		                          ("excludeFromCoinjoin" ,(coin-excluded-from-coinjoin? coin)))))
		              (wallet-unspent-coins wallet)))

		       (define (full-wallet-info wallet)
		         (let* ((segwit-pubkey (wallet-segwit-extpubkey wallet))
		                (taproot-pubkey (wallet-taproot-extpubkey wallet))
		                (has-taproot? (and taproot-pubkey
		                                   (not (string-empty? (extpubkey->string taproot-pubkey)))))
		                (segwit-account
		                  `(("name" "segwit")
		                    ("publicKey" ,(extpubkey->string segwit-pubkey))
		                    ("keyPath" ,(native->string (wallet-segwit-account-keypath wallet)))))
		                (taproot-account
		                  `(("name" "taproot")
		                    ("publicKey" ,(extpubkey->string taproot-pubkey))
		                    ("keyPath" ,(native->string (wallet-taproot-account-keypath wallet)))))
		                (accounts (if has-taproot?
		                              (list segwit-account taproot-account)
		                              (list segwit-account)))
		                (base-info
		                  `(("walletName" ,(wallet-name wallet))
		                    ("walletFile" ,(wallet-path wallet))
		                    ("loaded" ,(wallet-loaded? wallet))
		                    ("masterKeyFingerprint" ,(wallet-master-key-fingerprint wallet))
		                    ("anonScoreTarget" ,(wallet-anonscore-target wallet))
		                    ("isWatchOnly" ,(wallet-watch-only? wallet))
		                    ("isHardwareWallet" ,(wallet-hardware-wallet? wallet))
		                    ("isAutoCoinjoin" ,(wallet-auto-coinjoin? wallet))
		                    ("isNonPrivateCoinIsolation" ,(wallet-non-private-coin-isolation? wallet))
		                    ("accounts" ,accounts))))
		           (if (wallet-loaded? wallet)
		               (append base-info
		                       `(("balance" ,(wallet-balance wallet))
		                         ("coinjoinStatus" "unknown")))
		               base-info)))

		       (define (open-wallet wallet)
		         (wallet-info
		           (if (not (wallet-loaded? wallet))
		               (__start_wallet wallet)
		               wallet)))
		       """;
	}
}
