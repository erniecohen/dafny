// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// OMITTED-RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// OMITTED-RUN: %diff "%s.expect" "%t"

module Provider {
  type Pure(!new) = int
  function Zero(): Pure { 0 }
  export API provides Pure, Zero
}
module Client {
  import P = Provider`API
  newtype Wrap = P.Pure witness *
  type WrapPromise(!new) = Wrap
  lemma Inhabited() {
    var value := P.Zero() as Wrap;
    assert false;
  }
}
