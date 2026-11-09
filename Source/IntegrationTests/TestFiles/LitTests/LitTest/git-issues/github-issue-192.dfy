// Regression for https://github.com/erniecohen/dafny/issues/192: m[k := v][k] == v for
// bitvector keys, and the same for multiset elements.  --bitvector-map-keys keeps a
// bitvector key's type at a map or multiset lookup; off, as by default, nothing changes.
// The programs are in Inputs.
// RUN: echo "maps (refresh:false, keys:false)" > "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-192-maps.dfy" --type-system-refresh:false --bitvector-map-keys:false --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 >> "%t"
// RUN: echo "maps (refresh:false, keys:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-192-maps.dfy" --type-system-refresh:false --bitvector-map-keys:true --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 >> "%t"
// RUN: echo "multiset (refresh:false, keys:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-192-multiset.dfy" --type-system-refresh:false --bitvector-map-keys:false --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 >> "%t"
// RUN: echo "multiset (refresh:false, keys:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-192-multiset.dfy" --type-system-refresh:false --bitvector-map-keys:true --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 >> "%t"
// RUN: echo "multiset (refresh:true, keys:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-192-multiset.dfy" --type-system-refresh:true --bitvector-map-keys:true --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 >> "%t"
// RUN: echo "negative (refresh:false, keys:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-192-negative.dfy" --type-system-refresh:false --bitvector-map-keys:false --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 >> "%t"
// RUN: echo "negative (refresh:false, keys:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-192-negative.dfy" --type-system-refresh:false --bitvector-map-keys:true --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 >> "%t"
// RUN: %diff "%s.expect" "%t"
