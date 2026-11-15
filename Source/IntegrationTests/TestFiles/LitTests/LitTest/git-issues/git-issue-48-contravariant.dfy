// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false --allow-axioms --use-basename-for-filename --show-snippets:false "%s" > "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --allow-axioms --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

function incompatible(): int -> bool {
  if true then (x: bool) => true else (x: int) => true
}
