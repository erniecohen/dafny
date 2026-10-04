// Private compiled-type-test rejection draft, isolated from other resolver
// failures. Use --type-system-refresh --general-newtypes --extended-newtype-bases.
datatype Maybe<T> = None | Some(value: T)
newtype Phantom<T> = Maybe<int> witness None

method CannotRecoverPhantom(m: Maybe<int>) returns (b: bool) {
  b := m is Phantom<bool>; // ERROR: target T is absent from its instantiated carrier
}
