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

Four further constructions require proved `wellFounded` premises:

| Construction | Strict comparison |
| --- | --- |
| `LexicographicOf(r, bound)` | Lexicographic sequences of length at most `bound`; a proper prefix is smaller. A leading decrease may reset the tail. |
| `PointwiseOf(r)` | Equal-length finite sequences; each coordinate stays equal or descends, and some coordinate changes. |
| `ProductOf(r, s)` | Pairs; each coordinate stays equal or descends, and some coordinate changes. |
| `InverseImageOf(r, f)` | `x` descends to `y` exactly when `r(f(x), f(y))`. The total function `f` need not be injective. |

Each has a corresponding `*Gt` predicate and a proved `*WellFounded` lemma. No transitivity
assumption is needed. The product proof uses nested well-founded induction. Bounded lexicographic
comparison then inducts on the length bound. A pointwise descending chain has constant length,
so it descends in one such bounded lexicographic order. Inverse image maps a chain through `f`.
The existing accessibility lemmas supply the induction; the constructions add no axioms.

Without a length bound, lexicographic comparison with shorter prefixes below extensions is
not well-founded: `[1], [0, 1], [0, 0, 1], ...` descends over natural numbers. For pointwise
comparison different lengths are incomparable, so no common bound on all inputs is required.

The constructors and their supporting helpers publish checked `{:termination_closed}`
application summaries under `--decreases-orders`. They can be used in modular global and
callback bounds as well as ordinary decreases clauses. See
[`git-issue-245-instances.dfy`](../../IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-245-instances.dfy)
and its included modular clients for complete examples, including a lexicographic tail reset,
a pointwise update, either component of a product and a non-injective rank function.

The module is a library of its own, `binaries/DafnyStandardLibraries-orders.doo`, built
independently from built-in types. Dafny loads it with the standard library only
under `--decreases-orders`, `--type-system-refresh` and `--general-traits=datatype` (or
`full`), because a datatype implements the trait. Under `--modular-termination`, which does
not load the standard library, Dafny loads this library on its own. It is then verified with
the program that loads it, under that program's options, instead of being admitted by a
modular termination record. Programs that do not use the option are unaffected.

The source project uses the internal `--bootstrapping` mode to build the implementation
without loading its bundled copy. This retains the existing effects of `Of`, `wellFounded`
and `stops`; all construction summaries are checked. Consumers recheck the library normally.

Rebuild the binary with `make update-orders`, then rebuild Dafny to embed it. The lit tests
`git-issues/github-issue-245.dfy` and `git-issues/github-issue-211-multiset.dfy` verify the
library from these sources.
