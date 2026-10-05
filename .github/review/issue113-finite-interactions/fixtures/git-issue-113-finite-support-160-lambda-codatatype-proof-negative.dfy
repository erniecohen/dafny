// Source-only intended field-membership rejection. Positive is nonempty.
// The suspended lambda still returns the same co-family through a nominal view.
// This file makes no independent anti-vacuity claim from the invalid Bad definition.
type Positive = x: int | x > 0 witness 1
codatatype Outer = Outer(tail: Outer, thunk: () -> Outer, field: Positive)
newtype WrappedOuter = Outer witness *

function Bad(): WrappedOuter {
  Outer(Bad() as Outer, () => (Bad() as Outer), 0 as Positive) as WrappedOuter
}
