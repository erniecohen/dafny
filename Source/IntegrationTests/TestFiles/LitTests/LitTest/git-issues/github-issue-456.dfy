// RUN: echo "least-lemma, legacy resolver" > "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:false "%S/Inputs/github-issue-456-least-lemma.dfy" >> "%t"
// RUN: echo "least-lemma, refresh resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:true --general-traits:legacy "%S/Inputs/github-issue-456-least-lemma.dfy" >> "%t"
// RUN: echo "least-predicate, legacy resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:false "%S/Inputs/github-issue-456-least-predicate.dfy" >> "%t"
// RUN: echo "least-predicate, refresh resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:true --general-traits:legacy "%S/Inputs/github-issue-456-least-predicate.dfy" >> "%t"
// RUN: echo "co-recursion, legacy resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:false "%S/Inputs/github-issue-456-co-recursion.dfy" >> "%t"
// RUN: echo "co-recursion, refresh resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:true --general-traits:legacy "%S/Inputs/github-issue-456-co-recursion.dfy" >> "%t"
// RUN: python3 "%S/Inputs/github-issue-211-marked.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" 456 >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/456: under --modular-termination, three kinds of call were not checked
// against the 'decreases {:global}' bound of the caller, and each proved false in a program with no
// assumption:
//  - calls in the body of a least or greatest lemma, which is verified as its prefix lemma; the prefix
//    lemma had no bound (Inputs/github-issue-456-least-lemma.dfy);
//  - calls of a least or greatest predicate, and of its prefix predicate (-least-predicate.dfy);
//  - co-recursive calls (-co-recursion.dfy).
// A prefix lemma now has the bound of its least or greatest lemma, prefix predicates and lemmas stand for
// their declarations in the call graph of the option, and these calls get the same checks as other calls.
// The driver then checks the rules member by member (Inputs/github-issue-456-rules*.dfy: every marked
// member must be rejected) and the control Inputs/github-issue-456-accepted.dfy, whose only errors are its
// 'assert false' controls.
