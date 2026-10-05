// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Maybe<T> = None | Some(value: T)
newtype Phantom<T> = Maybe<int> witness None

method CannotRecoverPhantom(m: Maybe<int>) returns (b: bool) {
  b := m is Phantom<bool>; // ERROR: target T is absent from its instantiated carrier
}
