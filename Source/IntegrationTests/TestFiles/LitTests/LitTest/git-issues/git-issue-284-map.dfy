// https://github.com/erniecohen/dafny/issues/284
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:false --legacy-noncanonical-map-payloads:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:false --legacy-noncanonical-map-payloads:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:true --legacy-noncanonical-map-payloads:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:true --legacy-noncanonical-map-payloads:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

lemma IsetEquality()
  ensures iset { 0, 1, 2 } == iset { 2, 1, 0 }
{}

lemma IsetNegative() {
  assert iset { 0, 1, 2 } == iset { 2, 1, 0 };
  assert false;
}
