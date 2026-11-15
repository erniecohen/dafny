// RUN: %baredafny verify --solver-path "%z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%s" > "%t"
// RUN: %baredafny verify --solver-path "%z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:true "%s" >> "%t"
// RUN: %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%s" >> "%t"
// RUN: %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:true "%s" >> "%t"
// Vacuity control, with both resolvers: the same program with `assert false` after the assertion fails there, and only there.
// RUN: %exits-with 4 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%S/Inputs/github-issue-98-control.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:true "%S/Inputs/github-issue-98-control.dfy" >> "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
//
// An explicit trigger on a forall statement instantiated for a closure-valued local (#98,
// upstream dafny-lang/dafny#6036).  The trigger term and the asserted term were the same,
// `iChain(T, ot, c1)`, but the instantiation needs the closure's type membership,
// `$Is(c1, TotalFunc1(nat, T))`, which this line before #160 could not derive: its fact about the
// captured `c` bound an int, `Requires1(.., c, $Box(i + j))`, while the closure's body applies `c`
// to `$Box($Unbox(bx) + j)`.  The lambda source facts of #160 state it at the boxed argument,
// with triggers on the closure's Apply1 and Requires1, and the assertion verifies.
// Every positive RUN line discriminates: at 854f5a6~1 (just before #160) each of the four
// combinations (the harness Z3 4.12.1 and Z3 5.1.0, each with both resolvers) reports
// `(8,4): Error: assertion might not hold` and `1 verified, 1 error`.
//
// CHECK-NOT: Error
// CHECK: Dafny program verifier finished with 2 verified, 0 errors
// CHECK-NOT: Error
// CHECK: Dafny program verifier finished with 2 verified, 0 errors
// CHECK-NOT: Error
// CHECK: Dafny program verifier finished with 2 verified, 0 errors
// CHECK-NOT: Error
// CHECK: Dafny program verifier finished with 2 verified, 0 errors
// CHECK: github-issue-98-control\.dfy\(10,4\): Error: assertion might not hold
// CHECK: Dafny program verifier finished with 1 verified, 1 error
// CHECK: github-issue-98-control\.dfy\(10,4\): Error: assertion might not hold
// CHECK: Dafny program verifier finished with 1 verified, 1 error

type Ord<!T(!new)> = (T,T)->bool
ghost predicate iChain<T(!new)>(o:Ord<T>,c:nat->T) { forall n:nat :: o(c(n),c(n+1)) }
ghost predicate wf<T(!new)>(o:Ord<T>) { forall c:nat->T :: !iChain(o,c) }
type WfO<!T(!new)> = o:Ord<T> | wf(o) witness *
method test<S(!new),T(!new)>(ot:WfO<T>,c:nat->(S,T),j:nat) {
    var c1:nat->T := (i:nat) => c(i+j).1;
    forall c1:nat->T {:trigger iChain(ot,c1)} ensures !iChain(ot,c1) {}
    assert !iChain(ot,c1);
}
