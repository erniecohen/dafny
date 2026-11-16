# Well-founded orders

`Orders.dfy` is the module `Std.Termination.Orders`, the well-founded orders of decreases
components `e by O` (https://github.com/erniecohen/dafny/issues/245; Reference Manual,
section "Well-founded orders").

- `Wfo<T>` is a trait. A value has a relation `gt(x, y)`, "`x` is greater than `y`", and a
  member `aha(c)` whose postcondition gives a place where the chain `c` stops descending.
  So the `gt` of every value of `Wfo<T>` is well-founded.
- `Of(r)` is the order whose `gt` is `r`, for a relation `r` proved `wellFounded`.
- `WfoOf` is the datatype that implements `Of`.
- `Chain` and `WfoOf` are marked `{:termination_closed}`, so that under `--modular-termination`
  other modules may use them in bounds.
- `MultisetOf(r)` is the multiset order over a well-founded relation `r`, in the form of Huet
  and Oppen that VeriFast uses (https://github.com/erniecohen/dafny/issues/211): `M` is at least
  `N` (`MultisetGe`) when each element of `N - M` is below some element of `M - N`, and above it
  (`MultisetGt`) when also `M != N`. Each such step is a step of the Dershowitz-Manna order, whose
  well-foundedness is proved here by accessibility, as in Nipkow's proof. `MultisetRel(r)` is the
  relation itself, so multiset orders nest. The lemmas, with the names of the corresponding
  lemmas of VeriFast's `termination.gh` in the comments, prove the usual steps and, when `r` is
  transitive, transitivity and both absorption laws (`MultisetGtGe`, `MultisetGeGt`).

The module is a library of its own, `binaries/DafnyStandardLibraries-orders.doo`, built
against `binaries/DafnyStandardLibraries.doo`. Dafny loads it with the standard library only
under `--decreases-orders`, `--type-system-refresh` and `--general-traits=datatype` (or
`full`), because a datatype implements the trait. Under `--modular-termination`, which does
not load the standard library, Dafny loads this library on its own. It is then verified with
the program that loads it, under that program's options, instead of being admitted by a
modular termination record. Programs that do not use the option are unaffected.

Rebuild the binary with `make update-orders`, then rebuild Dafny to embed it. The lit tests
`git-issues/github-issue-245.dfy` and `git-issues/github-issue-211-multiset.dfy` verify the
library from these sources.
