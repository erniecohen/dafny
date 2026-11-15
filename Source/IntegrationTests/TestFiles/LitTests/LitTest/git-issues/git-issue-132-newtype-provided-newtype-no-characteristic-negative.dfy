// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.axioms-on.expect" "%t"

module Provider {
  type Pure(!new) = int
  newtype HiddenPure = Pure witness 0
  export API provides Pure, HiddenPure
}
module Client {
  import P = Provider`API
  newtype Id<T> = T witness *
  type AcceptDeclaredPromise(!new) = P.Pure
  type RejectHiddenNewtype(!new) = P.HiddenPure
  type RejectHiddenActual(!new) = Id<P.HiddenPure>
}
