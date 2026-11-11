# Consistent decreases tuples

This fixes https://github.com/erniecohen/dafny/issues/264 using the construction in
https://github.com/erniecohen/dafny/issues/211#issuecomment-6806579143.
The corrected comparison is the default, by explicit owner decision for this
soundness repair. The hidden `--legacy-decreases-order` switch restores the old,
unsound translation, including modular checks, for migration and measurement.
`--modular-termination` remains a separate, experimental opt-in feature.
No primitive axiom is added. The fix changes generated assertions, the loop
summary assumption, and the relation restricting induction hypotheses. The loop
summary uses the compatible weak tuple relation.

## Component relations

Write `s(x,y)` for new `x` strictly below old `y`, and `w(x,y)` for weak
nonincrease. Every pair has well-founded `s`, reflexive/transitive `w`,
`s` contained in `w`, and both absorption laws `s;w` and `w;s` contained in `s`.
The following are semantic arguments relative to the existing encodings, not
new axioms or a mechanized proof of the translator.

* **Integers:** `s(x,y) = 0 <= y && x < y`,
  `w(x,y) = x == y || (0 <= y && x <= y)`.
  Ordinary transitivity of numeric comparison proves transitivity and absorption;
  any nonidentity weak step has a nonnegative old value. A strict chain has only
  finitely many nonnegative integer values, followed by at most one negative
  terminal value. Equality at negative values is allowed; further descent is not.
* **Reals:** use the same `w`, with `s(x,y) = 0 <= y && x <= y - 1`.
  Adding a weak inequality on either side preserves the unit gap and the old
  lower bound. Each strict step spends at least one unit of a nonnegative bound;
  after at most finitely many such steps it reaches a terminal negative value.
  A fractional decrease can precede suffix progress, but cannot erase a reset.
* **Booleans, characters and bitvectors:** embed into `{0,1}`, Unicode character
  codes, and unsigned integers of a fixed bit width, respectively. Use `<` and
  `<=`. Their finite natural ranges prove all laws and well-foundedness.
  Different bit widths are incomparable. References use the Boolean nullness
  embedding (`null` below non-null).
* **Finite sets:** proper subset and subset. Transitivity and either absorption
  law follow from inclusion and a retained witness of strictness. Cardinality
  strictly decreases, establishing well-foundedness. Finite maps use their domains.
* **Multisets:** proper pointwise submultiset and pointwise submultiset. A strict
  witness remains strict through either composition; finite total multiplicity
  strictly decreases. This is the existing submultiset order, not the more general
  multiset extension of another order.
* **Sequences and inductive datatypes:** embed *all* these representations into
  the same natural-valued structural rank (`Seq#Rank` or `DtRank`), and use `<`
  and `<=` there. Equality of ranks always admits the suffix, including across
  representations. Strict rank descent is well-founded and the numeric laws
  prove absorption, even through alternating sequence/datatype representations.
* **ORDINAL:** ordinary ordinal `<` and its reflexive closure. Ordinal order is
  transitive and well-founded, so both absorption laws hold, including at limits.
* **Equality-only families:** infinite sets/maps, arrows, abstract values and
  coinductive datatypes have `s = false` and equivalence as `w`. The laws hold
  trivially. Coinductive values are not assigned an inductive descent order.
* **TOP:** every proper value is strictly and weakly below TOP; TOP is weakly
  below only itself. No proper component is above TOP. This extends each pair
  while preserving its laws. Incompatible proper families have neither relation.

The private iterator yield counter retains its reverse integer comparison. This
is a productivity marker: increasing it records a yield/return from `MoveNext`,
not an infinite execution within one invocation. Between yields it is constant,
so the remaining well-founded components prove progress as usual. Its arithmetic
strict/weak pair is transitive and absorbing; its use does not assert that the
reverse order on all integers is well-founded.

## Tuple lift and call sites

After conceptual TOP padding, generate right to left:

```
L_end = false                  N_end = true
L_i = s_i || (w_i && L_(i+1))   N_i = s_i || (w_i && N_(i+1))
```

The implementation translates only the comparable shared prefix. Its terminal
Boolean is exactly the first TOP comparison (or false at an incompatible pair),
then applies the recurrence above. It never turns shared-prefix nonincrease
into permission to discard a proper suffix. Numeric lower bounds are part of
`s` and `w`; an earlier weak step cannot discharge a later lower bound.

Calls, loops (including their free summary invariant), overrides, induction,
`decreases to`/`nonincreases to`, M2 global-bound checks, and M3 callback
application/forwarding all use this helper. M2 and forwarding request weak
comparison directly; they no longer simulate it by appending a dummy component.
The old `AtMost` adapter and equality-controlled implementation are reachable
only under the hidden legacy switch.

The induction callers use the same well-founded strict relation. In particular,
a nonnegative numeric argument can admit a negative terminal induction argument;
the old induction-only lower bound on the new value excluded that case. These
additional hypotheses follow by well-founded induction on the component/tuple
rankings just described. A negative old numeric component cannot itself justify
further numeric descent. Thus the repair changes assumptions as well as checks;
its soundness claim relies on these order arguments, not just on removing facts.

For transitivity and absorption, choose the earlier decisive strict coordinate
of the two comparisons. Every preceding coordinate composes weakly, and the
component absorption laws preserve that strict coordinate. If neither comparison
is strict, all coordinates compose weakly. Thus the lifted weak relation is a
preorder containing strict comparison and both absorption laws hold.

For each finite program composition, tuple arity has a finite maximum. In an
infinite strict chain choose the least coordinate decisive infinitely often.
After finitely many earlier decisions, that coordinate takes only weak steps
and infinitely many strict steps. Absorption compresses the intervening weak
steps to an infinite component strict chain, contradicting well-foundedness.
This argument does not claim well-foundedness for unrestricted first-class
variable-length tuples.

## Regression and measurement boundary

`git-issues/git-issue-264.dfy` checks the varying-arity real reset, the original
proof of `false`, the equal-rank mixed override, the fixed-arity M2 reset, and
M3 callback forwarding. Positive controls cover fractional weak progress,
negative terminal values, TOP padding and an early strict step with an increasing
suffix; negative controls include a reachable `assert false`. The `laws` fixture
also verifies arbitrary real-tuple and alternating sequence/datatype-rank
absorption/transitivity, and natural-valued ranking functions for the numeric
strict relations. These focused algebraic checks do not prove translator coverage.

The Actions comparison records default versus legacy suite program verdicts,
individual declaration outcomes, proof-batch resource units, and standard-library
declaration verdicts/resources. Differences are reported by name. It also checks
old-source default against candidate legacy without normalizing solver text or
output. Expected-verdict updates are taken from the Actions artifacts in their
own commit, with a reason for each change.
