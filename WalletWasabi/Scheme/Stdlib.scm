;;; Scheme Standard Library
;;; A collection of essential Scheme procedures and macros

;;; ----------------------
;;; Core Syntax and Macros
;;; ----------------------

;; Define a basic conditional form that expands to nested if expressions
;; Usage: (cond (test1 expr1) (test2 expr2) ... (else exprN))
(define-macro (cond h . t)
  (if (null? h)
      nil
      (begin
        (define test1 (if (equal? (car h) 'else) '#t (car h)))
        (define expr1 (car (cdr h)))
        `(if ,test1 ,expr1 (cond ,@t)))))

;; Define the case form that allows matching a value against multiple literals
;; Usage: (case key ((val1 val2) expr1) ((val3) expr2) (else expr3))
(define-macro (case key . clauses)
  (let ((key-var (gensym "key")))
    `(let ((,key-var ,key))
       ,(let process-clauses ((clauses clauses))
          (if (null? clauses)
              #f  ; No matching clause found
              (let* ((clause (car clauses))
                     (datums (car clause))
                     (exprs (cdr clause)))
                (if (eq? datums 'else)
                    `(begin ,@exprs)
                    `(if ,(if (pair? datums)
                              `(or ,@(map (lambda (datum)
                                            `(eqv? ,key-var ',datum))
                                          datums))
                              `(eqv? ,key-var ',datums))
                         (begin ,@exprs)
                         ,(process-clauses (cdr clauses))))))))))

;; Define the let form for local variable bindings
;; Supports both normal and named (recursive) forms
;; Usage: (let ((var1 val1) (var2 val2)) expr) or (let name ((var1 val1)) expr)
(define-macro (let first . rest)
  (if (symbol? first)
      ;; Named let form
      `(letrec ((,first (lambda ,(map car (car rest))
                          ,@(cdr rest))))
         (,first ,@(map cadr (car rest))))
      ;; Regular let form
      `((lambda ,(map car first)
          ,@rest)
        ,@(map cadr first))))

;; Define the let* form for sequential variable bindings
;; Usage: (let* ((var1 val1) (var2 val2-using-var1)) expr)
(define-macro (let* bindings . body)
  (if (null? bindings)
      `((lambda () ,@body))
      `(let (,(car bindings))
         (let* ,(cdr bindings) ,@body))))

;; Define the do form for iteration
;; Usage: (do ((var1 init1 step1) (var2 init2 step2)) (test result) command...)
(define-macro (do bindings test-and-result . commands)
  (let ((loop-var (gensym 'loop))
        (vars (map car bindings))
        (inits (map cadr bindings))
        (steps (map (lambda (binding)
                      (if (null? (cddr binding))
                          (car binding)  ; use the variable itself if no step
                          (caddr binding)))
                    bindings))
        (test (car test-and-result))
        (result (cdr test-and-result)))

    ;; Create temporary variables to hold the initial values
    (let ((temp-vars (map (lambda (v) (gensym (symbol->string v))) vars)))
      `(let ,(map (lambda (temp init)
                    (list temp init))
                  temp-vars inits)
         (let ,loop-var ,(map (lambda (var temp)
                                (list var temp))
                              vars temp-vars)
              (if ,test
                  (begin ,@result)
                  (begin
                    ,@commands
                    (,loop-var ,@steps))))))))

;; Define a while loop for imperative-style iteration
;; Usage: (while test body)
(define-macro (while test body)
  `(letrec
     ((loop
        (lambda ()
          (if ,test
              (begin ,body (loop))
              nil))))
     (loop)))

;;; ------------------
;;; Variadic operations
;;; ------------------
(define (variadic_math_op op identity unary args)
  (define (op-all acc remaining)
    (if (null? remaining)
      acc
      (op-all (op acc (car remaining))
        (cdr remaining))))
  (cond
    ((null? args) identity)                   ; + with no args returns 0
    ((null? (cdr args)) (* unary (car args)))    ; + with one arg returns that arg
    (else (op-all (car args) (cdr args)))))

(define (+ . args) (variadic_math_op $math_op_+ 0 1 args))
(define (- . args) (variadic_math_op $math_op_- 0 -1 args))
(define (* . args) (variadic_math_op $math_op_* 1 1 args))
(define (/ . args) (variadic_math_op $math_op_/ 1 1 args))

(define (variadic-comparison compare-op args)
  (define (all-compare? prev items)
    (cond
      ((null? items) #t)
      ((compare-op prev (car items))
        (all-compare? (car items) (cdr items)))
      (else #f)))
  (cond
    ((null? args) #t)                  ; = with no args returns #t
    ((null? (cdr args)) #t)            ; = with one arg returns #t
    (else (all-compare? (car args) (cdr args)))))

(define (= . args) (variadic-comparison $compare_= args))
(define (> . args) (variadic-comparison $compare_> args))
(define (< . args) (variadic-comparison $compare_< args))
(define (<= . args) (variadic-comparison compare_<= args))
(define (>= . args) (variadic-comparison compare_>= args))


;;; ------------------
;;; List Manipulation
;;; ------------------

;; Define an empty list constant
(define nil '())

;; Create a list from the given arguments
;; Usage: (list 1 2 3) => (1 2 3)
(define (list . xs) xs)

;; Check if an object is a list
(define (list? xs)
  (or (null? xs)
      (and (pair? xs)
           (list? (cdr xs)))))

;; Check if an object is an atom (non-pair)
(define (atom? xs)
  (not (pair? xs)))

;; Combine two lists into a single list
;; Usage: (append '(1 2) '(3 4)) => (1 2 3 4)
(define append
  (lambda args
    (cond ((null? args) '())
          ((null? (cdr args)) (car args))
          (else
           (let loop ((list1 (car args))
                      (remaining (cdr args)))
             (cond ((null? remaining) list1)
                   ((null? list1) (loop (car remaining) (cdr remaining)))
                   (else
                    (cons (car list1)
                          (loop (cdr list1) remaining)))))))))

;; Access an element at a specific position in a list (0-based indexing)
;; Returns #f if index is out of bounds
(define (list-ref lst n)
  (cond
    ((null? lst) #f)
    ((= n 0) (car lst))
    ((< n 0) #f)
    (else (list-ref (cdr lst) (- n 1)))))

;; Return a sublist starting at a specific position
;; Usage: (list-tail '(a b c) 1) => (b c)
(define (list-tail lst k)
  (if (zero? k)
      lst
      (list-tail (cdr lst) (- k 1))))

;; Return the length of a list
(define (length xs)
  (if (null? xs) 0
      (+ 1 (length (cdr xs)))))

;; Create a list of numbers from l (inclusive) to r (exclusive)
;; Usage: (range 1 4) => (1 2 3)
(define (range l r)
  (if (= l r) '()
      (cons l (range (+ 1 l) r))))

;; Filter a list to only include elements that satisfy a predicate
;; Usage: (filter even? '(1 2 3 4)) => (2 4)
(define (filter f xs)
  (if (null? xs) '()
      (if (f (car xs))
          (cons (car xs) (filter f (cdr xs)))
          (filter f (cdr xs)))))

;; Reverse the order of elements in a list
;; Usage: (reverse '(1 2 3)) => (3 2 1)
(define (reverse xs) (fold cons nil xs))

;; Pair corresponding elements from two lists into a list of lists
;; Usage: (zip '(a b) '(1 2)) => ((a 1) (b 2))
(define (zip a b)
  (if (or (null? a) (null? b))
      '()
      (cons (list (car a) (car b)) (zip (cdr a) (cdr b)))))

;; Check if all elements in a list are distinct
;; Bug: Uses undefined member? function, should be member
(define (distinct? lst)
  (if (null? lst)
      #t
      (if (member? (car lst) (cdr lst))
          #f
          (distinct? (cdr lst)))))

;; Create a list excluding all elements in the first list from the second list
;; Bug: Uses undefined member? function, should be member
(define (exclude items lst)
  (if (null? lst)
      nil
      (if (member? (car lst) items)
          (exclude items (cdr lst))
          (cons (car lst) (exclude items (cdr lst))))))

(define (take lst n)
  (cond ((= n 0) '())
    ((null? lst) '())
    (else (cons (car lst)
            (take (cdr lst) (- n 1))))))

(define (drop lst n)
  (cond ((= n 0) lst)
    ((null? lst) '())
    (else (drop (cdr lst) (- n 1)))))

(define (sort lst compare-proc)
  (define (insert x sorted-lst)
    (cond ((null? sorted-lst) (list x))
      ((compare-proc x (car sorted-lst)) (cons x sorted-lst))
      (else (cons (car sorted-lst) (insert x (cdr sorted-lst))))))

  (define (insertion-sort lst)
    (if (null? lst)
      '()
      (insert (car lst) (insertion-sort (cdr lst)))))

  (insertion-sort lst))

;;; ------------------
;;; Higher-Order List Functions
;;; ------------------

;; Apply a function to each element of a list, returning a new list
;; Usage: (map sqrt '(1 4 9)) => (1 2 3)
(define (map proc ls . lol)
  (define (map1 proc ls res)
    (if (pair? ls)
        (map1 proc (cdr ls) (cons (proc (car ls)) res))
        (if (null? ls)
            (reverse res)
            (error "map: improper list" ls))))
  (define (mapn proc lol res)
    (if (every pair? lol)
        (mapn proc
              (map1 cdr lol '())
              (cons (apply proc (map1 car lol '())) res))
        (if (every (lambda (x) (if (null? x) #t (pair? x))) lol)
            (reverse res)
            (error "map: improper list in list" lol))))
  (if (null? lol)
      (map1 proc ls '())
      (mapn proc (cons ls lol) '())))

;; Apply a function to each element of a list for side effects only
;; Usage: (for-each display '(1 2 3))
(define (for-each f ls . lol)
  (define (for1 f ls)
    (if (pair? ls)
        (begin (f (car ls)) (for1 f (cdr ls)))
        (if (not (null? ls))
            (error "for-each: improper list" ls))))
  (if (null? lol)
      (for1 f ls)
      (begin (apply map f ls lol) (if #f #f))))

;; Left-to-right fold (reduce) operation on a list
;; Usage: (foldl + 0 '(1 2 3)) => 6
(define (foldl f a xs)
  (if (null? xs) a
      (foldl f (f (car xs) a) (cdr xs))))

;; Right-to-left fold (reduce) operation on a list
;; Usage: (foldr cons '() '(1 2 3)) => (1 2 3)
(define (foldr f end lst)
  (if (null? lst)
      end
      (f (car lst) (foldr f end (cdr lst)))))

;; Aliases for fold operations
(define fold foldl)
(define reduce foldr)

;; Search for the first element in a list that satisfies a predicate
;; Usage: (find even? '(1 3 4 5)) => 4
(define (find pred lst)
  (cond
    ((null? lst) #f)
    ((pred (car lst)) (car lst))
    (else (find pred (cdr lst)))))

;; Check if any element in a list satisfies a predicate
;; Usage: (any even? '(1 3 5 6)) => #t
(define (any pred ls . lol)
  (define (any1 pred ls)
    (if (pair? (cdr ls))
        ((lambda (x) (if x x (any1 pred (cdr ls)))) (pred (car ls)))
        (pred (car ls))))
  (define (anyn pred lol)
    (if (every pair? lol)
        ((lambda (x) (if x x (anyn pred (map cdr lol))))
         (apply pred (map car lol)))
        #f))
  (if (null? lol)
      (if (pair? ls) (any1 pred ls) #f)
      (anyn pred (cons ls lol))))

;; Check if every element in a list satisfies a predicate
;; Usage: (every odd? '(1 3 5)) => #t
(define (every pred ls . lol)
  (define (every1 pred ls)
    (if (null? (cdr ls))
        (pred (car ls))
        (if (pred (car ls)) (every1 pred (cdr ls)) #f)))
  (if (null? lol)
      (if (pair? ls) (every1 pred ls) #t)
      (not (apply any (lambda xs (not (apply pred xs))) ls lol))))

;;; ------------------
;;; List Access Shortcuts
;;; ------------------

;; Common list element access functions
(define (cadr xs) (car (cdr xs)))
(define (caddr xs) (cadr (cdr xs)))
(define (cadddr xs) (caddr (cdr xs)))
(define (caddddr xs) (cadddr (cdr xs)))
(define (cadddddr xs) (caddddr (cdr xs)))
(define (caddddddr xs) (cadddddr (cdr xs)))

(define (caar xs) (car (car xs)))
(define (cddr xs) (cdr (cdr xs)))

;; Aliases for common list accessors
(define first car)
(define second cadr)
(define third caddr)
(define last list-tail)

;;; ------------------
;;; Equality and Membership Testing
;;; ------------------

;; Check if two values are equivalent
;; Combines eq? with numeric equality
(define (eqv? a b)
  (if (eq? a b) #t (and (number? a) (equal? a b))))

;; Check if an item is a member of a list using the provided comparer
(define (memx item lst eq)
  (if (null? lst) #f
      (if (eq item (car lst)) lst
          (memx item (cdr lst) eq))))

;; Check if an item is a member of a list (using equal?)
;; Usage: (member 'b '(a b c)) => (b c)
(define (member item lst) (memx item lst equal?))

;; Check if an item is a member of a list (using eq?)
;; Usage: (memq 'b '(a b c)) => (b c)
(define (memq item lst) (memx item lst eq?))

;; Check if an item is a member of a list (using eqv?)
;; Usage: (memv 'b '(a b c)) => (b c)
(define (memv item lst) (memx item lst eqv?))

;; Look up a key in an association list (using the given comparer)
(define (associate key alist eq)
  (cond ((null? alist) #f)
    ((eq key (caar alist)) (car alist))
    (else (associate key (cdr alist) eq))))

;; Look up a key in an association list (using eq?)
;; Usage: (assq 'b '((a . 1) (b . 2))) => (b . 2)
(define (assq key alist)
  (associate key alist eq?))

;; Look up a key in an association list (using equal?)
;; Bug: Uses assq in else clause instead of recursively calling assoc
(define (assoc key alist)
  (associate key alist equal?))

;; Look up a key in an association list (using eqv?)
;; Bug: Uses assv in else clause instead of recursively calling assoc
(define (assv key alist)
  (associate key alist eqv?))

;;; ------------------
;;; Functional Programming Utilities
;;; ------------------

;; Create a curried function
;; Bug: Uses fc which is undefined, should be fn
(define (curry fn arg1)
  (lambda (arg) (apply fc (cons arg1 (list arg)))))

;; Compose two functions
;; Usage: (define f+g (compose f g))
(define (compose f g)
  (lambda (arg) (f (apply g arg))))

;; Logical negation
;; Usage: (not #t) => #f
(define (not x) (if x #f #t))

;; Check if an argument is false
;; Usage: (false? #f) => #t
(define (false? x) (not x))

;; Exclusive or operation
;; Usage: (xor #t #f) => #t
(define (xor a b) (and (or a b) (not (and a b))))

;; Check if a value is a boolean
;; Usage: (boolean? #t) => #t
(define (boolean? x) (if (eq? x #t) #t (eq? x #f)))

;; Create a delayed computation (lazy evaluation)
;; Usage: (define d (delay (expensive-computation)))
(define (delay expr) (lambda () expr))

;; Force evaluation of a delayed computation
;; Usage: (force d)
(define (force thunk) (thunk))

;;; ------------------
;;; Numeric Operations
;;; ------------------

;; Check if a number is zero
(define (zero? x) (= 0 x))

;; Check if a number is positive (>=0)
(define (positive? x) (> x 0))

;; Check if a number is negative
(define (negative? x) (< x 0))

;; Increment a number by 1
(define (inc x) (+ 1 x))

;; Decrement a number by 1
(define (dec x) (- x 1))

;; Calculate the absolute value of a number
(define (abs x) (if (< x 0) (* -1 x) x))

;; Check if a number is odd
(define (odd? x) (not (even? x)))

;; Check if a number is even
(define (even? x) (= 0 (remainder x 2)))

;; Calculate the square of a number
(define (square x) (* x x))

;; Calculate b raised to the power of n
;; Usage: (expt 2 3) => 8
(define (expt base exponent)
  (cond
    ;; Handle division by zero error case - raising 0 to a negative power
    ((and (zero? base) (negative? exponent))
      (error "expt: cannot raise 0 to a negative power"))

    ;; Handle 0^0 = 1 by convention
    ((and (zero? base) (zero? exponent))
      1)

    ;; Handle positive exponents with standard recursive approach
    ((>= exponent 0)
      (expt-iter base exponent 1))

    ;; Handle negative exponents as 1/(base^abs(exponent))
    (else
      (/ 1 (expt-iter base (- exponent) 1)))))

;; Helper function using tail recursion for efficiency
(define (expt-iter b e r)
  (if (= 0 e)
      r
      (if (even? e)
          (expt-iter (square b) (/ e 2) r)
          (expt-iter b (- e 1) (* r b)))))

;; Sum all elements in a list or all arguments
;; Usage: (sum 1 2 3) => 6 or (sum '(1 2 3)) => 6
(define (sum . lst) (fold + 0 lst))

;; Calculate the product of all arguments
;; Usage: (product 2 3 4) => 24
(define (product . lst) (fold * 1 lst))

;; Compute the remainder of division
;; Bug: This defines remainder in terms of itself, creating infinite recursion
(define (remainder x y) (% x y))

;; Compute the modulo (always returns a non-negative result)
;; Bug: Uses the buggy remainder function above
(define (modulo x y)
  (define remainder (remainder x y))
  (if (or
       (and (negative? remainder) (positive? y))
       (and (positive? remainder) (negative? y)))
      (+ remainder y)
      remainder))

;; Greater than or equal comparison
(define (compare_>= a b) (or (> a b) (= a b)))

;; Less than or equal comparison
(define (compare_<= a b) (or (< a b) (= a b)))

;; Find the maximum value among arguments
(define (max x . rest)
  (if (null? rest)
      x
      (let ((m (apply max rest)))
        (if (> x m) x m))))

;; Find the minimum value among arguments
(define (min x . rest)
  (if (null? rest)
      x
      (let ((m (apply min rest)))
        (if (< x m) x m))))

;; Extract the numerator of a rational number
;; Note: Depends on exact and normalize which are not defined here
(define (numerator x)
  (if (integer? x)
      x
      (let ((rat (exact x)))
        (if (integer? rat)
            rat
            (* (sign rat) (car (normalize rat)))))))

;; Extract the denominator of a rational number
;; Note: Depends on exact and normalize which are not defined here
(define (denominator x)
  (if (integer? x)
      1
      (let ((rat (exact x)))
        (if (integer? rat)
            1
            (cadr (normalize rat))))))

;; Calculate the greatest common divisor of multiple numbers
(define (gcd . args)
  (cond ((null? args) 0)
        ((null? (cdr args)) (abs (car args)))
        (else (let ((a (car args))
                    (b (cadr args))
                    (rest (cddr args)))
                (apply gcd (cons (gcd-two-numbers a b) rest))))))

;; Helper function for gcd calculation
(define (gcd-two-numbers a b)
  (if (= b 0)
      (abs a)
      (gcd-two-numbers b (remainder a b))))

;; Calculate the least common multiple of multiple numbers
(define (lcm . args)
  (cond ((null? args) 1)
        ((null? (cdr args)) (abs (car args)))
        (else (let ((a (car args))
                    (b (cadr args))
                    (rest (cddr args)))
                (apply lcm (cons (lcm-two-numbers a b) rest))))))

;; Helper function for lcm calculation
(define (lcm-two-numbers a b)
  (if (or (= a 0) (= b 0))
      0
      (abs (/ (* a b) (gcd a b)))))

(define (decimal-part n) (% n 1))
(define (integer-part n) (- n (decimal-part n)))
(define truncate integer-part)

(define (quotient n d)
  (/ (- n (remainder n d)) d))

(define (floor x)
  (cond
    ;; If x is already an integer, return it
    ((= (decimal-part x) 0) x)

    ;; For positive numbers, truncate toward zero
    ((>= x 0) (truncate x))

    ;; For negative numbers with a decimal part, subtract 1 from truncated value
    (else (- (truncate x) 1))))

(define (ceiling x)
  (cond
    ;; If x is already an integer, return it
    ((= (decimal-part x) 0) x)

    ;; For negative numbers, truncate toward zero
    ((< x 0) (truncate x))

    ;; For positive numbers with a decimal part, add 1 to truncated value
    (else (+ (truncate x) 1))))


(define (round x)
  (cond
    ;; If x is already an integer, return it
    ((= (decimal-part x) 0) x)

    ;; Special case for negative 0.5 (round to 0)
    ((= x -0.5) 0)

    ;; Handle the specific case of x.5 (round to even)
    ((= (abs (decimal-part x)) 0.5)
      (let ((truncated (truncate x)))
        (if (= (modulo truncated 2) 0)
          ;; If truncated value is even, round to it
          truncated
          ;; If truncated value is odd, round away from zero
          (if (> x 0)
            (+ truncated 1)
            (- truncated 1)))))

    ;; For decimal part < 0.5, round toward zero (truncate)
    ((< (abs (decimal-part x)) 0.5)
      (truncate x))

    ;; For decimal part > 0.5, round away from zero
    (else
      (if (> x 0)
        (+ (truncate x) 1)
        (- (truncate x) 1)))))
;;; ------------------
;;; I/O Operations
;;; ------------------

;; Print a newline character
(define newline (lambda () (display "\r\n")))

;; Display text without a newline
(define (write text) (display text))

;; Display text followed by a newline
(define (writeln text) (display text) (newline))

;;; ------------------
;;; Numbers
;;; ------------------
(define complex? number?)
(define real? number?)
(define rational? number?)
(define (integer? n)
  (and (number? n) (= (remainder n 1) 0)))

(define (exact? _) #t)
(define (inexact? _) #f)

;;; ------------------
;;; Strings
;;; ------------------
(define (string-length str)
  (length (string->list str)))

(define (string-empty? str)
  (eq? 0 (string-length str)))

(define (string=? a b)
  (equal?
    (string->list a)
    (string->list b)))

(define (substring str start end)
  (list->string
    (take (drop (string->list str) start)
      (- end start))))

(define (string-append . strings)
  (list->string
   (let loop ((remaining strings)
              (result '()))
     (if (null? remaining)
         result
         (loop (cdr remaining)
               (append result (string->list (car remaining))))))))

(define (make-string n char)
  (list->string
    (let loop ((count 0) (result '()))
      (if (= count n)
        (reverse result)
        (loop (+ count 1) (cons char result))))))

(define (string-join separator string-list)
  (cond
    ((null? string-list) "")
    ((null? (cdr string-list)) (car string-list))
    (else
      (let loop ((result (car string-list))
                  (rest (cdr string-list)))
        (if (null? rest)
          result
          (loop (string-append result separator (car rest))
            (cdr rest)))))))

;;; ------------------
;;; Error Handling
;;; ------------------

;; Simple continuation to top-level (not active by default)
(define escape nil)
;; Uncomment to enable: (call/cc (lambda (c) (set! escape c)))

;; Simple error reporting mechanism
;; Displays error message and escapes to top-level if escape is defined
(define (error (msg)) (display msg) (escape nil))
