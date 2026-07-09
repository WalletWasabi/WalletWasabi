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

(define-accessor hdpubkey-pubkey pubkey)
(define-accessor hdpubkey-keypath fullkeypath)
(define-accessor hdpubkey-labels labels)
(define-accessor hdpubkey-state keystate)
(define-accessor hdpubkey-index index)
(define-accessor hdpubkey-internal? isinternal)

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
  (let* ((keypath (__get 'fullkeypath key))
         (indexes (__get 'indexes keypath))
         (purpose (car indexes)))
    (if (= purpose 86)
        (__get 'P2Taproot key)
        (__get 'P2wpkhScript key))))

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
;;; Wallet Accessors
;;; ----------------------

(define-accessor wallet-name walletname)
(define-accessor wallet-keymanager keymanager)
(define-accessor wallet-loaded? loaded)
(define-accessor wallet-transactions gettransactions)

(define-accessor wallet-path filepath wallet-keymanager)
(define-accessor wallet-watch-only? IsWatchOnly wallet-keymanager)
(define-accessor wallet-hardware-wallet? IsHardwareWallet wallet-keymanager)
(define-accessor wallet-auto-coinjoin? AutoCoinjoin wallet-keymanager)
(define-accessor wallet-non-private-coin-isolation? NonPrivateCoinIsolation wallet-keymanager)
(define-accessor wallet-anonscore-target anonscoretarget wallet-keymanager)
(define-accessor wallet-excluded-coins-from-coinjoin excludedcoinsfromcoinjoin wallet-keymanager)

(define (wallet-master-key-fingerprint wallet)
  (native->string (__get 'masterfingerprint (wallet-keymanager wallet))))

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
;;; Transaction Accessors
;;; ----------------------

(define-accessor transaction-wallet-inputs walletinputs)
(define-accessor transaction-wallet-outputs walletoutputs)
(define-accessor transaction-foreign-inputs foreigninputs)
(define-accessor transaction-foreign-outputs foreignoutputs)
(define-accessor transaction-raw transaction)
(define-accessor transaction-block-index blockindex)
(define-accessor transaction-labels labels)
(define-accessor transaction-first-seen firstseen)
(define-accessor transaction-replacement? isreplacement)
(define-accessor transaction-speedup? isspeedup)
(define-accessor transaction-cancellation? iscancellation)
(define-accessor transaction-cpfp? iscpfp)
(define-accessor transaction-confirmed? confirmed)
(define-accessor transaction-coinjoin? iswasabi2cj)

(define (transaction-hash tx)
  (native->string (__get 'gethash tx)))

(define (transaction-block-hash tx)
  (native->string (__get 'blockhash tx)))

(define (transaction-height tx)
  (height (__get 'height tx)))

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
;;; Outpoint Accessors
;;; ----------------------

(define-accessor outpoint-hash hash)
(define-accessor outpoint-n n)

;;; ----------------------
;;; Coin Accessors
;;; ----------------------

(define-accessor coin-tx transaction)
(define-accessor coin-outpoint outpoint)
(define-accessor coin-anonymityset anonymityset)
(define-accessor coin-spent? isspent)
(define-accessor coin-spent-by spendertransaction)
(define-accessor coin-confirmed? confirmed)
(define-accessor coin-banned? isbanned)
(define-accessor coin-banned-until banneduntilutc)
(define-accessor coin-script-pubkey scriptpubkey)
(define-accessor coin-script-pubkey-type scripttype)
(define-accessor coin-excluded-from-coinjoin? isexcludedfromcoinjoin)
(define-accessor coin-pubkey hdpubkey)

(define (coin-amount coin)
  (satoshi->bitcoin (__get 'satoshi (__get 'amount coin))))

(define (coin-height coin)
  (height (__get 'height coin)))

(define (coin-unspent? coin)
  (not (coin-spent? coin)))

(define (coin-cluster coin)
  (__get 'cluster (coin-pubkey coin)))

(define (coin-labels coin)
  (__get 'labels (coin-cluster coin)))

(define (coin-keypath coin)
  (__get 'fullkeypath (coin-pubkey coin)))

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

(define network      (__get 'network (global)))
(define header-chain (__get 'filterheaders (global)))

(define (remote-tip-height) (__get 'servertipheight header-chain))
(define (local-tip-height)  (__get 'tipheight header-chain))
(define (local-tip-hash)    (__get 'tiphash header-chain))
(define (headers-count)     (__get 'hashcount header-chain))
(define (headers-left)      (__get 'hashesleft header-chain))

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
  (let* ((km (wallet-keymanager wallet))
         (segwit-pubkey (__get 'segwitextpubkey km))
         (taproot-pubkey (__get 'taprootextpubkey km))
         (has-taproot? (and taproot-pubkey
                            (not (string-empty? (extpubkey->string taproot-pubkey)))))
         (segwit-account
           `(("name" "segwit")
             ("publicKey" ,(extpubkey->string segwit-pubkey))
             ("keyPath" ,(native->string (__get 'segwitaccountkeypath km)))))
         (taproot-account
           `(("name" "taproot")
             ("publicKey" ,(extpubkey->string taproot-pubkey))
             ("keyPath" ,(native->string (__get 'taprootaccountkeypath km)))))
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
