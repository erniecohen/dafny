// RUN: echo "positive, option off" > "%t"
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-196-positive.dfy" >> "%t"
// RUN: echo "positive, option on" >> "%t"
// RUN: %exits-with 0 %baredafny verify --use-basename-for-filename --show-snippets:false --quantified-check-call-facts "%S/Inputs/github-issue-196-positive.dfy" >> "%t"
// RUN: echo "positive, option on, Z3 5.1.0" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --quantified-check-call-facts "%S/Inputs/github-issue-196-positive.dfy" >> "%t"
// RUN: echo "positive, option on, refreshed type system" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --quantified-check-call-facts --type-system-refresh --general-traits=full --general-newtypes "%S/Inputs/github-issue-196-positive.dfy" >> "%t"
// RUN: echo "negative, option off" >> "%t"
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-196-negative.dfy" >> "%t"
// RUN: echo "negative, option on" >> "%t"
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false --quantified-check-call-facts "%S/Inputs/github-issue-196-negative.dfy" >> "%t"
// RUN: echo "negative, option on, Z3 5.1.0" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --quantified-check-call-facts "%S/Inputs/github-issue-196-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
//
// A universally quantified postcondition or assertion whose body calls a function failed when the
// solver simplified the checked instance until no trigger term remained, as l[i] - l[i] becomes
// 0.0: the call facts of the body were assumed in a separate quantifier, triggered on Seq#Index,
// so abs(0.0) stayed uninterpreted (https://github.com/erniecohen/dafny/issues/196).
// With --quantified-check-call-facts, the checked body of such a quantifier has its call facts as
// an antecedent; the quantifier assumed afterwards is the original.  With the option off, nothing
// changes.  The negative file shows that wrong bounds, `assert false` and calls outside their
// precondition still fail.
