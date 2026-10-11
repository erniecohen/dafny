// RUN: %exits-with 4 %verify --type-system-refresh=true --additional-axioms=false --cores=1 --resource-limit=16000000 --verification-time-limit=60 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --additional-axioms=true --cores=1 --resource-limit=16000000 --verification-time-limit=60 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Box<T> = Box(value:T)

function Good(): Box<nat>
  ensures Good().value == 0
{
  Box<int>.Box(0) as Box<nat>
}

lemma InhabitedControl()
{
  var b := Good();
  assert b.value == 0;
  assert b.value >= 0;
  assert false;
}
