# The `Ordinal` module

The `Ordinal` module defines several axioms and utilities for working with the `ORDINAL` type.

At the time of writing this, Dafny does not include that many operations and axioms
for working with ordinals.
Most of the axioms it does include only apply when `IsNat` is true for at least one argument.
This module includes several more that apply to all ordinals,
and defines the `Times` operation since multiplication with `*` is not currently supported.
These were invaluable in providing the foundation for the `Termination` module
and may be useful for other applications.

`Omega`, `PlusLimit` and `TimesLimit` are ghost functions, defined as
least elements from the lemmas of [Std.Axioms.Ordinals](Axioms.md), with their
postconditions proved.  The module's export set provides them without their
bodies, so an importer sees only their postconditions, as it did when they
were axioms, and the helpers that prove them are not exported.
