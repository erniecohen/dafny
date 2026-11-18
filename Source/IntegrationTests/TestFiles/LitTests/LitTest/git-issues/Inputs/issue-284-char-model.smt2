; https://github.com/erniecohen/dafny/issues/284
; A concrete model of the repaired character fragment on an infinite carrier.
; Interpret the erased character carrier as Int and both conversions as identity.
; Membership retains the actual valid-range predicate in each character mode.
; This is a model of this fragment only, not of the complete Dafny prelude.
(set-logic AUFLIA)
(define-fun |char#IsChar| ((unicode Bool) (n Int)) Bool
  (and (<= 0 n) (< n (ite unicode 1114112 65536))
       (or (not unicode) (< n 55296) (<= 57344 n))))
(define-fun |char#FromInt| ((n Int)) Int n)
(define-fun |char#ToInt| ((ch Int)) Int ch)
(define-fun IsChar ((unicode Bool) (ch Int)) Bool (|char#IsChar| unicode ch))
(assert (forall ((unicode Bool) (n Int))
  (=> (|char#IsChar| unicode n) (= (|char#ToInt| (|char#FromInt| n)) n))))
(assert (forall ((unicode Bool) (ch Int))
  (=> (IsChar unicode ch)
    (and (= (|char#FromInt| (|char#ToInt| ch)) ch) (|char#IsChar| unicode (|char#ToInt| ch))))))
(assert (forall ((unicode Bool) (ch Int))
  (= (IsChar unicode ch)
    (and (|char#IsChar| unicode (|char#ToInt| ch)) (= (|char#FromInt| (|char#ToInt| ch)) ch)))))
(check-sat)
; A typed input exists in both modes, so a negative assertion has a countermodel.
(push 1)
(declare-const bmpChar Int)
(assert (IsChar false bmpChar))
(check-sat)
(pop 1)
(push 1)
(declare-const scalarChar Int)
(assert (IsChar true scalarChar))
(assert (not (IsChar true 55296)))
(assert (not (IsChar true 57343)))
(check-sat)
(pop 1)
