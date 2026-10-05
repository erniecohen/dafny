// RUN: %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
function Natural(n: nat): nat
  ensures Natural(n) == n
  decreases n
{
  if n == 0 then 0 else 1 + Natural(n - 1)
}
lemma NaturalWorks(n: nat) ensures Natural(n) == n {}
