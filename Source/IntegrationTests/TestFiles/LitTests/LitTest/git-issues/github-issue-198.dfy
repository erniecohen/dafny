// A recursive member predicate on a constructed term, in a precondition, under --additional-axioms
// (https://github.com/erniecohen/dafny/issues/198): the second conjunct of the inlined body gets the
// body's #canCall facts, so it no longer depends on what else is in the program. Both declaration orders.
// RUN: echo "forms (additional axioms, refreshed)" > "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-forms.dfy" --additional-axioms --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "forms (additional axioms, legacy)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-forms.dfy" --additional-axioms --type-system-refresh:false --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "reversed (additional axioms, refreshed)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-reversed.dfy" --additional-axioms --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "reversed (additional axioms, legacy)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-reversed.dfy" --additional-axioms --type-system-refresh:false --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (additional axioms, refreshed)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-198-controls.dfy" --additional-axioms --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (additional axioms, legacy)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-198-controls.dfy" --additional-axioms --type-system-refresh:false --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: %diff "%s.expect" "%t"
