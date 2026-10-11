// RUN: %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.axioms-on.expect" "%t"

module Provider {
  type Opaque = int
  newtype Wrapped = int witness 0
  export API reveals Opaque, Wrapped
}
module Client {
  import P = Provider`API
  newtype Outer = P.Wrapped witness 0
  type RevealedAlias(!new) = P.Opaque
  type RevealedNewtype(!new) = P.Wrapped
  type RevealedOuter(!new) = Outer
}
