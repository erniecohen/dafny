// Invariant datatype constructor arguments under the opt-in --invariant-constructor-inference
// (https://github.com/erniecohen/dafny/issues/101).
// RUN: echo "positive (legacy)" > "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh:false --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-101-positive.dfy" >> "%t"
// RUN: echo "positive (refreshed, off)" >> "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh:true --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-101-positive.dfy" >> "%t"
// RUN: echo "positive (refreshed, on)" >> "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh:true --invariant-constructor-inference --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-101-positive.dfy" >> "%t"
// RUN: echo "negative (refreshed, off)" >> "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh:true --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-101-negative.dfy" >> "%t"
// RUN: echo "negative (refreshed, on)" >> "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh:true --invariant-constructor-inference --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-101-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
