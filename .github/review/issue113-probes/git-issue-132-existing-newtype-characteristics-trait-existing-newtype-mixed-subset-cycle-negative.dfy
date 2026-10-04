// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
// Existing cycle rejection control; no extension-only recursive semantics.
newtype N = S
type S = x: N | true witness *
