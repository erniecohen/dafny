// A recursive member predicate on a constructed term, in a precondition, under --inlined-body-call-permissions
// (https://github.com/erniecohen/dafny/issues/198): the second conjunct of the inlined body gets the
// body's #canCall facts, so it no longer depends on what else is in the program. Both declaration orders.
// RUN: echo "forms (refreshed)" > "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-forms.dfy" --inlined-body-call-permissions --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "forms (legacy)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-forms.dfy" --inlined-body-call-permissions --type-system-refresh:false --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "forms (with --additional-axioms, refreshed)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-forms.dfy" --inlined-body-call-permissions --additional-axioms --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "reversed (refreshed)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-reversed.dfy" --inlined-body-call-permissions --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "reversed (legacy)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-198-reversed.dfy" --inlined-body-call-permissions --type-system-refresh:false --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (refreshed)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-198-controls.dfy" --inlined-body-call-permissions --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (legacy)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-198-controls.dfy" --inlined-body-call-permissions --type-system-refresh:false --solver-path "%review-z3" --cores:1 --resource-limit:16000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: %diff "%s.expect" "%t"
