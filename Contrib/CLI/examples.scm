;; Returns a list of wallet UTXOs, each annotated with hop-distance info,
;; sorted by number of hops since last coinjoin (descending).
(define (list-wallet-coins-sorted-by-hops walletname)
  (let* (;; Look up the wallet object by its name
          (wallet (get-wallet-by-name walletname))
          ;; Get all unspent coins (UTXOs) belonging to this wallet
          (utxos  (wallet-unspent-coins wallet))
          ;; Get all transactions associated with this wallet
          (txs    (wallet-transactions wallet)))

    ;; Given a coin, find the transaction that created it by matching
    ;; the coin's outpoint hash against each transaction's hash.
    (define (coin-tx coin) (__get 'transaction coin))

    ;; Recursively count how many transaction "hops" back we need to go
    ;; from a given coin until we reach either:
    ;;   - a coinjoin transaction (privacy-enhancing mix), or
    ;;   - the edge of the wallet's known transaction history.
    ;; `counter` accumulates the hop count as we walk backwards.
    (define (hops-until-coinjoin coin counter best-so-far)
      (if (>= counter best-so-far)
        best-so-far
        (let ((tx (coin-tx coin)))
          ;; Base case: if no transaction found or it's a coinjoin, stop here
          (if (or (null? tx) (transaction-coinjoin? tx))
            counter
            ;; Otherwise, look at the coins consumed (spent) by this transaction
            (let ((destroyed-coins (transaction-wallet-inputs tx)))
              (if (null? destroyed-coins)
                ;; No wallet inputs means we've reached the boundary — stop
                counter
                (fold (lambda (c current-best)
                        (min current-best
                          (hops-until-coinjoin c (+ 1 counter) current-best)))
                  best-so-far
                  destroyed-coins)))))))

    ;; Convert a coin into an association-list-style info structure
    ;; containing its outpoint, amount, hop count, and labels.
    (define (coin->info c)
      (list (list "outpoint" (native->string (coin-outpoint c)))
            (list "amount"   (coin-amount c))
            (list "hops"     (hops-until-coinjoin c 0 999999))
            (list "labels"   (coin-labels c))))

    ;; Comparator: sort by hop count in descending order.
    ;; Coins with MORE hops since their last coinjoin appear first,
    ;; highlighting the ones most in need of a remix for privacy.
    (define (by-hop-count-desc a b)
      (> (second (third a)) (second (third b))))

    ;; Build the final result: annotate each UTXO and sort
    (sort (map coin->info utxos) by-hop-count-desc)))


;; Returns a list of entries showing the wallet balance after each transaction,
;; ordered chronologically (by block height and block index).
;; Each entry contains the transaction hash, height, and running balance.
(define (historical-balance walletname)
  (let* ((wallet (get-wallet-by-name walletname))
          (txs    (wallet-transactions wallet)))

    ;; Compute the net effect of a transaction on the wallet balance.
    ;; Wallet outputs are coins received (increase balance),
    ;; wallet inputs are coins spent (decrease balance).
    (define (tx-net-effect tx)
      (let ((received (apply sum (map coin-amount (transaction-wallet-outputs tx))))
             (spent    (apply sum (map coin-amount (transaction-wallet-inputs tx)))))
        (- received spent)))

    ;; Sort transactions chronologically:
    ;; first by block height (ascending), then by block index (ascending).
    ;; Unconfirmed transactions (non-numeric height) are placed at the end.
    (define (tx-before? a b)
      (let ((ha (transaction-height a))
             (hb (transaction-height b)))
        (cond
          ;; Both confirmed — compare heights, break ties with block index
          ((and (number? ha) (number? hb))
            (if (= ha hb)
              (< (transaction-block-index a) (transaction-block-index b))
              (< ha hb)))
          ;; Only a is confirmed — a comes first
          ((number? ha) #t)
          ;; Only b is confirmed — b comes first
          ((number? hb) #f)
          ;; Neither confirmed — order by first-seen time
          (else #f))))

    (define sorted-txs (sort txs tx-before?))

    ;; Walk through sorted transactions, accumulating a running balance.
    ;; Returns a list of (hash, height, balance-after) entries.
    (define (build-history txs running-balance)
      (if (null? txs)
        '()
        (let* ((tx          (car txs))
                (new-balance (+ running-balance (tx-net-effect tx)))
                (entry       (list (list "hash"    (transaction-hash tx))
                                   (list "height"  (transaction-height tx))
                                   (list "balance" new-balance))))
          (cons entry (build-history (cdr txs) new-balance)))))

    (build-history sorted-txs 0)))


;; Export wallet coin labels in BIP-329 format.
;;
;; BIP-329 defines a standard JSON Lines format for exporting
;; labels associated with transactions, outputs, and addresses.
;;
;; This function takes a wallet name, retrieves all coins (UTXOs)
;; belonging to that wallet, and returns a list of label records
;; suitable for serialization to JSON Lines.
;;
;; Each record is an association list with the following fields:
;;   - "type"  : Always "output", since we are labeling UTXOs.
;;   - "ref"   : The outpoint (txid:vout) identifying the UTXO.
;;   - "label" : A space-separated string of all labels attached
;;               to the coin.
;;
;; Parameters:
;;   walletname - A string identifying the wallet by name.
;;
;; Returns:
;;   A list of association lists, one per labeled coin, conforming
;;   to the BIP-329 schema.
;;
;; Example:
;;   (export-bip329 "my-wallet")
;;   ;; => ((("type" "output")
;;   ;;      ("ref" "abc123...def:0")
;;   ;;      ("label" "savings cold-storage"))
;;   ;;     (("type" "output")
;;   ;;      ("ref" "789fed...012:1")
;;   ;;      ("label" "change")))

(define (export-bip329 walletname)
  ;; Look up the wallet once and bind it for use in the body.
  (let ((wallet (get-wallet-by-name walletname)))

    ;; Convert a single coin into a BIP-329 label record.
    ;;
    ;; Parameters:
    ;;   c - A coin object representing a UTXO in the wallet.
    ;;
    ;; Returns:
    ;;   An association list with "type", "ref", and "label" keys.
    (define (coin-export c)
      (list (list "type"  "output")
            (list "ref"   (native->string (coin-outpoint c)))
            (list "label" (string-join " " (coin-labels c)))))

    ;; Apply coin-export to every coin in the wallet and return
    ;; the resulting list of label records.
    (map coin-export (wallet-coins wallet))))
