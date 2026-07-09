;;; Curated port of WalletWasabi's Scheme/Stdlib.scm for NScheme.
;;;
;;; This file contains ONLY the parts of the original standard library that
;;; NScheme does not already provide natively. Everything the interpreter
;;; supplies as a primitive or a special form (arithmetic, comparisons,
;;; append/length/list-ref/map/reverse, cond/case/let/let*/do, the numeric
;;; tower, most string/char/vector operations, ...) is intentionally omitted.
;;;
;;; Some originally-ported procedures are now provided natively by the
;;; interpreter (Builtins.cs) because they are hot and ubiquitous, so they are
;;; NOT redefined here: filter, for-each, foldl/foldr (plus R6RS fold-left/
;;; fold-right), member/memq/memv, assoc/assq/assv, and sort (native sort is a
;;; stable O(n log n) merge sort). `fold`/`reduce` below are thin aliases onto
;;; the native folds.
;;;
;;; Differences from the original, and why (see StdlibTests.cs for coverage):
;;;   * Every recursive list procedure that remains here is written
;;;     tail-recursively, so it runs in constant C# stack. The originals (range,
;;;     zip, take, ...) recursed in non-tail position and overflowed on large
;;;     input.
;;;   * `while` is a hygienic syntax-rules macro (was an unhygienic
;;;     define-macro). `delay`/`force` are fixed: the original `delay` was a
;;;     *procedure*, so its argument was evaluated eagerly and nothing was lazy.
;;;   * Several outright bugs in the original are corrected here: `last` (was an
;;;     alias for list-tail, returning a sublist instead of the last element),
;;;     `curry` (referenced an undefined `fc`), `compose` (applied its argument
;;;     as a list). See the header of StdlibTests.cs for the full catalogue.

;;; ------------------
;;; Constants / predicates
;;; ------------------

(define nil '())

;; An atom is anything that is not a pair.
(define (atom? x) (not (pair? x)))

;;; ------------------
;;; List manipulation (all tail-recursive)
;;; ------------------

;; (range lo hi) -> (lo ... hi-1). Built from the top down so the list is
;; assembled with cons in tail position — no reverse, constant stack.
(define (range lo hi)
  (let loop ((i (- hi 1)) (acc '()))
    (if (< i lo)
        acc
        (loop (- i 1) (cons i acc)))))

;; Pair corresponding elements of two lists: (zip '(a b) '(1 2)) => ((a 1) (b 2))
(define (zip a b)
  (let loop ((a a) (b b) (acc '()))
    (if (or (null? a) (null? b))
        (reverse acc)
        (loop (cdr a) (cdr b) (cons (list (car a) (car b)) acc)))))

;; The first n elements of xs (or all of them, if fewer than n).
(define (take xs n)
  (let loop ((xs xs) (n n) (acc '()))
    (if (or (= n 0) (null? xs))
        (reverse acc)
        (loop (cdr xs) (- n 1) (cons (car xs) acc)))))

;; xs without its first n elements.
(define (drop xs n)
  (if (or (= n 0) (null? xs))
      xs
      (drop (cdr xs) (- n 1))))

;; (list-tail '(a b c) 1) => (b c)
(define (list-tail xs k)
  (if (zero? k)
      xs
      (list-tail (cdr xs) (- k 1))))

;; #t when no element repeats.
(define (distinct? xs)
  (cond ((null? xs) #t)
        ((member (car xs) (cdr xs)) #f)
        (else (distinct? (cdr xs)))))

;; xs with every element that also appears in `items` removed.
(define (exclude items xs)
  (let loop ((xs xs) (acc '()))
    (cond ((null? xs) (reverse acc))
          ((member (car xs) items) (loop (cdr xs) acc))
          (else (loop (cdr xs) (cons (car xs) acc))))))

;;; ------------------
;;; Folds and searches
;;; ------------------

;; `fold`/`reduce` are the traditional names for the native left/right folds.
(define fold foldl)
(define reduce foldr)

;; First element satisfying pred, or #f.
(define (find pred xs)
  (cond ((null? xs) #f)
        ((pred (car xs)) (car xs))
        (else (find pred (cdr xs)))))

;; (any pred l1 l2 ...) walks the lists in parallel and returns the first
;; non-#f (pred e1 e2 ...), else #f. `(member '() lists)` is the "some list is
;; exhausted" test. Recurs in tail position.
(define (any pred first . rest)
  (let loop ((lists (cons first rest)))
    (if (member '() lists)
        #f
        (let ((result (apply pred (map car lists))))
          (if result
              result
              (loop (map cdr lists)))))))

;; (every pred l1 l2 ...) — #f as soon as (pred ...) fails, otherwise the last
;; truthy result (so it doubles as "and over a list"). Tail-recursive.
(define (every pred first . rest)
  (let loop ((lists (cons first rest)) (last #t))
    (if (member '() lists)
        last
        (let ((result (apply pred (map car lists))))
          (if result
              (loop (map cdr lists) result)
              #f)))))

;;; ------------------
;;; List access shortcuts
;;; ------------------

(define (cadr x) (car (cdr x)))
(define (cddr x) (cdr (cdr x)))
(define (caddr x) (car (cddr x)))
(define (cadddr x) (car (cdr (cddr x))))
(define (caar x) (car (car x)))

(define first car)
(define second cadr)
(define third caddr)

;; The last element (NOT a tail — the original aliased this to list-tail, which
;; is a different function entirely).
(define (last xs)
  (if (null? (cdr xs))
      (car xs)
      (last (cdr xs))))

;;; ------------------
;;; Functional utilities
;;; ------------------

(define (xor a b) (and (or a b) (not (and a b))))

;; (compose f g) — a procedure that pipes all its arguments through g, then f.
;; The original applied its single argument *as a list*, which broke ordinary
;; one-value composition.
(define (compose f g)
  (lambda args (f (apply g args))))

;; Partially apply a two-argument procedure. The original referenced an
;; undefined `fc` (a typo for `fn`), so every call errored.
(define (curry fn arg1)
  (lambda (arg) (fn arg1 arg)))

;;; ------------------
;;; Lazy evaluation — delay must be a macro, not a procedure
;;; ------------------

;; A memoising promise: the thunk runs at most once. `delay` is hygienic, so the
;; `forced?`/`value` it introduces cannot capture names from the delayed
;; expression.
(define (make-promise thunk)
  (let ((forced? #f) (value #f))
    (lambda ()
      (if forced?
          value
          (begin (set! value (thunk))
                 (set! forced? #t)
                 value)))))

(define-syntax delay
  (syntax-rules ()
    ((_ expr) (make-promise (lambda () expr)))))

(define (force promise) (promise))

;;; ------------------
;;; Iteration macro (hygienic)
;;; ------------------

;; (while test body ...) — evaluate body while test is true. Written with
;; syntax-rules so the internal loop name cannot clash with the caller's code;
;; the loop itself is tail-recursive.
(define-syntax while
  (syntax-rules ()
    ((_ test body ...)
     (let loop ()
       (when test
         body ...
         (loop))))))

;;; ------------------
;;; Small numeric helpers (the rest of the numeric tower is native)
;;; ------------------

(define (inc x) (+ 1 x))
(define (dec x) (- x 1))

(define (sum . xs) (foldl + 0 xs))
(define (product . xs) (foldl * 1 xs))

;; Integer and fractional parts, on top of the native `truncate`. The original
;; used an undefined `%` operator and then clobbered the native `truncate` with
;; a weaker alias; neither is reproduced here.
(define (integer-part n) (truncate n))
(define (decimal-part n) (- n (truncate n)))

;;; ------------------
;;; Strings (only the non-native helpers)
;;; ------------------

(define (string-empty? s) (zero? (string-length s)))

;; Join a list of strings with a separator. Tail-recursive accumulation.
(define (string-join sep parts)
  (cond ((null? parts) "")
        ((null? (cdr parts)) (car parts))
        (else
         (let loop ((acc (car parts)) (rest (cdr parts)))
           (if (null? rest)
               acc
               (loop (string-append acc sep (car rest)) (cdr rest)))))))

;;; ------------------
;;; I/O helper
;;; ------------------

(define (writeln x) (display x) (newline))
