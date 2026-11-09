// An inherited trait predicate proved through an overriding function in another module, without a cast
// (https://github.com/erniecohen/dafny/issues/103), under --additional-axioms.
// RUN: echo "nocast (off)" > "%t"
// RUN: %exits-with 4 %baredafny verify --general-traits=datatype --use-basename-for-filename --show-snippets:false "%S/Inputs/git-issue-103-nocast.dfy" >> "%t"
// RUN: echo "nocast (additional axioms, legacy)" >> "%t"
// RUN: %exits-with 0 %baredafny verify --general-traits=datatype --additional-axioms --use-basename-for-filename --show-snippets:false "%S/Inputs/git-issue-103-nocast.dfy" >> "%t"
// RUN: echo "nocast (additional axioms, refreshed)" >> "%t"
// RUN: %exits-with 0 %baredafny verify --general-traits=datatype --additional-axioms --type-system-refresh --use-basename-for-filename --show-snippets:false "%S/Inputs/git-issue-103-nocast.dfy" >> "%t"
// RUN: echo "false (additional axioms)" >> "%t"
// RUN: %exits-with 4 %baredafny verify --general-traits=datatype --additional-axioms --use-basename-for-filename --show-snippets:false "%S/Inputs/git-issue-103-false.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
