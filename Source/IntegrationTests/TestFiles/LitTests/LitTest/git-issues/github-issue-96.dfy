// RUN: echo "positive, option off" > "%t"
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false --allow-warnings --allow-axioms "%S/Inputs/github-issue-96-positive.dfy" >> "%t"
// RUN: echo "positive, option on" >> "%t"
// RUN: %exits-with 0 %baredafny verify --use-basename-for-filename --show-snippets:false --allow-warnings --allow-axioms --statement-expression-conclusions "%S/Inputs/github-issue-96-positive.dfy" >> "%t"
// RUN: echo "positive, option on, Z3 5.1.0" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --allow-warnings --allow-axioms --statement-expression-conclusions "%S/Inputs/github-issue-96-positive.dfy" >> "%t"
// RUN: echo "project file (option on)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-96-project/dfyconfig.toml" --use-basename-for-filename --show-snippets:false >> "%t"
// RUN: echo "negative, option off" >> "%t"
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false --allow-warnings --allow-axioms "%S/Inputs/github-issue-96-negative.dfy" >> "%t"
// RUN: echo "negative, option on" >> "%t"
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false --allow-warnings --allow-axioms --statement-expression-conclusions "%S/Inputs/github-issue-96-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
//
// A statement expression S; E under a quantifier or a connective, in an assertion, kept only E:
// `assert forall n: nat :: assume false; false` failed (#96, upstream dafny-lang/dafny#1371).
// With --statement-expression-conclusions, such an S; E in the condition of an assert statement is
// translated as C ==> E (C && E where it occurs negatively), C being the statement's conclusion.
// With the option off, nothing changes.  The negative file shows that the statements are still
// checked, that conclusions do not make `assert false` or `ensures false` provable, that they
// hold only under the guards their well-formedness check had, that postconditions are not
// rewritten, and that old, old@L, fresh, unchanged, two-state lemmas and two-state functions
// (whose quantifiers range over another heap; review of PR 226) are left as they are.
