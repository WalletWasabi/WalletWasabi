(load "Scheme/Stdlib.scm")

;; Helpers
(define (bitcoin->satoshi n) (* n 100000000))
(define (satoshi->bitcoin n) (/ n 100000000.0))
(define (height native-height) (__get 'value native-height))

(define (get-wallet-by-name name)
  (define (by-name wallet)
    (string=? name (wallet-name wallet)))
  (find by-name (wallets)))

(define (get-opened-wallets)
  (filter wallet-loaded? (wallets)))

(define (wallet-name wallet) (__get 'walletname wallet))
(define (wallet-path wallet) (__get 'filepath (wallet-keymanager wallet)))
(define (wallet-keymanager wallet) (__get 'keymanager wallet))
(define (wallet-unspent-coins wallet) (filter coin-unspent? (wallet-coins wallet)))
(define (wallet-loaded? wallet) (__get 'loaded wallet))
(define (wallet-excluded-coins-from-coinjoin wallet) (__get 'excludedcoinsfromcoinjoin (wallet-keymanager wallet)))
(define (wallet-info wallet)
  (list
    (list "name"     (wallet-name wallet))
    (list "loaded"   (wallet-loaded? wallet))
    (list "readOnly" (wallet-watch-only? wallet))
    (list "path"     (wallet-path wallet))
    ))

(define (wallet-watch-only? wallet)      (__get 'IsWatchOnly (wallet-keymanager wallet)))
(define (wallet-hardware-wallet? wallet) (__get 'IsHardwareWallet (wallet-keymanager wallet)))
(define (wallet-auto-coinjoin? wallet)   (__get 'AutoCoinjoin (wallet-keymanager wallet)))
(define (wallet-non-private-coin-isolation? wallet) (__get 'NonPrivateCoinIsolation (wallet-keymanager wallet)))
(define (wallet-master-key-fingerprint wallet)   (native->string (__get 'masterfingerprint (wallet-keymanager wallet))))
(define (wallet-anonscore-target)        (__get 'anonscoretarget (wallet-keymanager wallet)))
(define (wallet-transactions wallet)     (__get 'gettransactions wallet))
(define (wallet-balance wallet)          (apply sum (map coin-amount (wallet-unspent-coins wallet))))

(define (transaction-hash tx)            (native->string (__get 'gethash tx)))
(define (transaction-wallet-inputs tx)   (__get 'walletinputs tx))
(define (transaction-wallet-outputs tx)  (__get 'walletoutputs tx))
(define (transaction-foreign-inputs tx)  (__get 'foreigninputs tx))
(define (transaction-foreign-outputs tx) (__get 'foreignoutputs tx))
(define (transaction-raw tx)             (__get 'transaction tx))
(define (transaction-height tx)          (height (__get 'height tx)))
(define (transaction-block-hash tx)      (native->string (__get 'blockhash tx)))
(define (transaction-block-index tx)     (__get 'blockindex tx))
(define (transaction-labels tx)          (__get 'labels tx))
(define (transaction-first-seen tx)      (__get 'firstseen tx))
(define (transaction-replacement? tx)    (__get 'isreplacement tx))
(define (transaction-speedup? tx)        (__get 'isspeedup tx))
(define (transaction-cancellation? tx)   (__get 'iscancellation tx))
(define (transaction-cpfp? tx)           (__get 'iscpfp tx))
(define (transaction-confirmed? tx)      (__get 'confirmed tx))
(define (transaction-coinjoin? tx)       (__get 'iswasabi2cj tx))
(define (transaction-info tx)
  (list
    (list "hash"           (transaction-hash tx))
    (list "height"         (transaction-height tx))
    (list "blockHash"      (transaction-block-hash tx))
    (list "isConfirmed"    (transaction-confirmed? tx))
    (list "isReplacement"  (transaction-replacement? tx))
    (list "isCancellation" (transaction-cancellation? tx))
    (list "isCoinjoin"     (transaction-coinjoin? tx))
    (list "isCpfp"         (transaction-cpfp? tx))
    (list "isSpeedup"      (transaction-speedup? tx))
    (list "firstSeen"      (transaction-first-seen tx))
    ))

(define (outpoint-hash outpoint)         (__get 'hash outpoint))
(define (outpoint-n outpoint)            (__get 'n outpoint))

(define (coin-amount coin)               (satoshi->bitcoin (__get 'satoshi (__get 'amount coin))))
(define (coin-outpoint coin)             (__get 'outpoint coin))
(define (coin-anonymityset coin)         (__get 'anonymityset coin))
(define (coin-height coin)               (height (__get 'height coin)))
(define (coin-spent? coin)               (__get 'isspent coin))
(define (coin-spent-by coin)             (__get 'spendertransaction coin))
(define (coin-unspent? coin)             (not (coin-spent? coin)))
(define (coin-confirmed? coin)           (__get 'confirmed coin))
(define (coin-banned? coin)              (__get 'isbanned coin))
(define (coin-banned-until coin)         (__get 'banneduntilutc coin))
(define (coin-script-pubkey coin)        (__get 'scriptpubkey coin))
(define (coin-script-pubkey-type coin)   (__get 'scripttype coin))
(define (coin-excluded-from-coinjoin? coin) (__get 'isexcludedfromcoinjoin coin))
(define (coin-pubkey coin)               (__get 'hdpubkey coin))
(define (coin-cluster coin)              (__get 'cluster (__get 'hdpubkey coin)))
(define (coin-labels coin)               (__get 'labels (coin-cluster coin)))
(define (coin-keypath coin)              (__get 'fullkeypath (coin-pubkey coin)))
(define (coin-address coin)              (native->string (script->address (coin-script-pubkey coin))))
(define (coin-info coin)
  (list
    (list "outpoint"       (native->string (coin-outpoint coin)))
    (list "amount"         (coin-amount coin))
    (list "labels"         (coin-labels coin))
    (list "anonymityScore" (coin-anonymityset coin))
    (list "confirmed"      (coin-confirmed? coin))
    (list "spent"          (coin-spent? coin))
    (list "keypath"        (native->string (coin-keypath coin)))
    (list "address"        (coin-address coin))
    ))

(define network                 (__get 'network (global)))
(define bitcoin-store           (__get 'bitcoinstore (global)))
(define header-chain            (__get 'smartheaderchain bitcoin-store))

(define (remote-tip-height)     (__get 'servertipheight header-chain))
(define (local-tip-height)      (__get 'tipheight header-chain))
(define (local-tip-hash)        (__get 'tiphash header-chain))
(define (headers-count)         (__get 'hashcount header-chain))
(define (headers-left)          (__get 'hashesleft header-chain))

;; RPC equivalent functions

(define (coin->rpc_info coin)
  (list
    (list "outpoint"       (native->string (coin-outpoint coin)))
    (list "amount"         (bitcoin->satoshi (coin-amount coin)))
    (list "anonymityScore" (coin-anonymityset coin))
    (list "confirmed"      (coin-confirmed? coin))
    (list "confirmations"  (- (remote-tip-height) (coin-height coin)))
    (list "keypath"        (native->string (coin-keypath coin)))
    (list "address"        (coin-address coin))
    ))

(define (unspent-coins wallet)
  (define (coin->internal-info coin)
    (append
      (coin->rpc_info coin)
      (list
        (list "labels"             (string-join ", " (coin-labels coin)))
        (list "exludeFromCoinjoin" (coin-excluded-from-coinjoin? coin)))))
  (map coin->internal-info (wallet-unspent-coins wallet)))


(define (full-wallet-info wallet)
  (define keymanager (wallet-keymanager wallet))

  ;; Helper function for creating account info
  (define (make-account-info name get-pubkey-fn get-keypath-fn)
    `(("name" ,name)
      ("publicKey" ,(extpubkey->string (get-pubkey-fn keymanager)))
      ("keyPath" ,(native->string (get-keypath-fn keymanager)))))

  ;; Account creators using the helper
  (define (segwit-account)
    (make-account-info
      "segwit"
      (lambda (km) (__get 'segwitextpubkey km))
      (lambda (km) (__get 'segwitaccountkeypath km))))

  (define (taproot-account)
    (make-account-info
      "taproot"
      (lambda (km) (__get 'taprootextpubkey km))
      (lambda (km) (__get 'taprootaccountkeypath km))))

  ;; Check if taproot is available
  (define taproot-pubkey (__get 'taprootextpubkey keymanager))
  (define taproot-available?
    (and taproot-pubkey (not (string-empty? (extpubkey->string taproot-pubkey)))))

  ;; Determine accounts list
  (define accounts
    (if taproot-available?
        (list (segwit-account) (taproot-account))
        (list (segwit-account))))

  ;; Create the base wallet info
  (define base-info
    `(("walletName" ,(wallet-name wallet))
      ("walletFile" ,(wallet-path wallet))
      ("loaded"     ,(wallet-loaded? wallet))
      ("masterKeyFingerprint" ,(wallet-master-key-fingerprint wallet))
      ("anonScoreTarget" ,(wallet-anonscore-target wallet))
      ("isWatchOnly" ,(wallet-watch-only? wallet))
      ("isHardwareWallet" ,(wallet-hardware-wallet? wallet))
      ("isAutoCoinjoin" ,(wallet-auto-coinjoin? wallet))
      ("isNonPrivateCoinsolation" ,(wallet-non-private-coin-isolation? wallet))
      ("accounts" ,accounts)))

  ;; Add additional info for started wallets
  (if (wallet-loaded? wallet)
      (append base-info
              `(("balance" ,(wallet-balance wallet))
                ("coinjoinStatus" "unknown")))
      base-info))

(define (open-wallet wallet)
  (wallet-info
    (if (not (wallet-loaded? wallet))
      (__start_wallet wallet)
      wallet)))
