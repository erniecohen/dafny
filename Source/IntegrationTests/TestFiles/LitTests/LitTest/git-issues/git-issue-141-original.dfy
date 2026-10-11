// RUN: %exits-with 4 %verify --type-system-refresh=true --additional-axioms=false --cores=1 --resource-limit=16000000 --verification-time-limit=60 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --additional-axioms=true --cores=1 --resource-limit=16000000 --verification-time-limit=60 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Box<T> = Box(value:T)

function Bad(): Box<nat>
  ensures Bad().value == -1
{
  Box<int>.Box(-1) as Box<nat>
}

lemma Contradiction()
  ensures false
{
  var b := Bad();
  assert b.value == -1;
  assert b.value >= 0;
  assert false;
}
