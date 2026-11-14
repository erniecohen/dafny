# Well-founded orders

`Orders.dfy` is the module `Std.Termination.Orders`, the well-founded orders of decreases
components `e by O` (https://github.com/erniecohen/dafny/issues/245; Reference Manual,
section "Well-founded orders").

- `Wfo<T>` is a trait. A value has a relation `gt(x, y)`, "`x` is greater than `y`", and a
  member `aha(c)` whose postcondition gives a place where the chain `c` stops descending.
  So the `gt` of every value of `Wfo<T>` is well-founded.
- `Of(r)` is the order whose `gt` is `r`, for a relation `r` proved `wellFounded`.
- `WfoOf` is the datatype that implements `Of`.

The module is a library of its own, `binaries/DafnyStandardLibraries-orders.doo`, built
against `binaries/DafnyStandardLibraries.doo`. Dafny loads it with the standard library only
under `--decreases-orders`, `--type-system-refresh` and `--general-traits=datatype` (or
`full`), because a datatype implements the trait. Programs that do not use the option are
unaffected.

Rebuild the binary with `make update-orders`, then rebuild Dafny to embed it. The lit test
`git-issues/github-issue-245.dfy` verifies the library from these sources.
