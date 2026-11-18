// RUN: echo "induction, legacy resolver" > "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:false "%S/Inputs/github-issue-460-induction.dfy" >> "%t"
// RUN: echo "induction, refresh resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:true --general-traits:legacy "%S/Inputs/github-issue-460-induction.dfy" >> "%t"
// RUN: echo "prefix-induction, legacy resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:false "%S/Inputs/github-issue-460-prefix-induction.dfy" >> "%t"
// RUN: echo "prefix-induction, refresh resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:true --general-traits:legacy "%S/Inputs/github-issue-460-prefix-induction.dfy" >> "%t"
// RUN: echo "limit, legacy resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:false "%S/Inputs/github-issue-460-limit.dfy" >> "%t"
// RUN: echo "limit, refresh resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:true --general-traits:legacy "%S/Inputs/github-issue-460-limit.dfy" >> "%t"
// RUN: echo "accepted, legacy resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:false "%S/Inputs/github-issue-460-accepted.dfy" >> "%t"
// RUN: echo "accepted, refresh resolver" >> "%t"
// RUN: %exits-with 4 %baredafny verify --warn-unchecked-termination=false --allow-warnings --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 --verification-time-limit 0 --boogie /normalizeDeclarationOrder:0 --type-system-refresh:true --general-traits:legacy "%S/Inputs/github-issue-460-accepted.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/460: under --modular-termination, a lemma with a
// 'decreases {:global}' bound that can reach dispatch to implementations in other modules runs under that
// bound, and so may any call it makes.  Three recursive instances of a lemma were not checked against it:
// the automatic induction hypothesis (Inputs/github-issue-460-induction.dfy), the induction hypothesis of
// the prefix lemma of a least or greatest lemma (-prefix-induction.dfy), and the case generated for a prefix
// lemma at a limit ordinal (-limit.dfy).  Each proved false in a program with no assumption.  Each of
// these hypotheses now covers only the instances whose bound is at most the bound of the lemma (below it,
// when the lemma is itself a member that such implementations may provide), so the three programs are
// rejected: the assertion that used the hypothesis, or the postcondition, is no longer proved.
// Inputs/github-issue-460-accepted.dfy uses the same hypotheses within the bound and still verifies; its
// only errors are its 'assert false' controls.
