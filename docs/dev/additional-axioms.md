# Additional verifier axioms

`--additional-axioms` is the shared, default-off switch for approved added-fact
families in this fork. The legacy spelling is `/additionalAxioms:1`.
In `dfyconfig.toml`:

```toml
[options]
additional-axioms = true
```

CLI `--additional-axioms=false` overrides a project setting. The normal project
option machinery supplies the setting to editor verification. The option is a
process/project verification setting (`OptionScope.Cli`), not a change to the
language or a `.doo` semantic compatibility requirement. Record its value in
proof-cost comparisons. It is distinct from `--allow-axioms`.

## Current family: bounded integer/bitvector round trips

For each registered positive width `w`, exactly one new axiom is emitted:

```boogie
axiom (forall x: int :: { nat_from_bvW(nat_to_bvW(x)) }
  0 <= x && x < M ==> nat_from_bvW(nat_to_bvW(x)) == x);
```

Here `W` stands for the concrete width and `M` for the exact integer numeral
`2^w`. For `bv32`, `M = 4294967296`; for `bv64`,
`M = 18446744073709551616`. The binder and intermediate have native integer
and bitvector sorts. The bound is computed with arbitrary-precision integers.
Registered widths include those introduced by shift-amount optimization.
There is no new axiom for `bv0`, whose inline conversions return zero.

### Derivation and combination with existing families

Interpret a width-`w` bitvector by its unsigned binary value
`U_w(b) = sum(bit_i(b) * 2^i, i = 0 .. w-1)`. The trusted native integer
conversion `I_w(x)` has unsigned value `x mod 2^w`, the nonnegative remainder.
The existing wrapper bridge says `N_w(b) = U_w(b)`. Thus for
`0 <= x < 2^w`, `N_w(I_w(x)) = x mod 2^w = x`.
These are the [SMT-LIB unsigned conversion semantics](https://smt-lib.org/theories-FixedSizeBitVectors.shtml).
The new family is entailed by the existing bridge and native semantics.
If `T` is that trusted theory, `S` any other enabled family, and `A33` this
family, `Models(T union S union A33) = Models(T union S)`. This shows the
addition imposes no new model restriction in combination with `S`; it does not
prove consistency of the entire prelude or of `S`. The shared switch currently
has no other families. Inventory this again when adding another family.

The strict upper bound and lower bound are essential: the total native round
trip is zero at `2^w`, and is `2^w - 1` at `-1`. Native conversions being total
does not make either Dafny cast legal. All existing source fit, integrality,
narrowing, boxing and allocation checks remain intact.

### Trigger and compatibility

The single explicit pattern is `N_w(I_w(x))`, with the existing uninterpreted
wrapper outermost. It matches round-trip terms, including terms exposed by
equalities. It does not trigger on every integer conversion and does not
introduce a structurally deeper conversion in its body. The guard restricts the
conclusion, not matching; false/unknown guards are covered by SMT controls.
The new quantifier has stable profiling id
`additional_axioms_bv<W>_int_round_trip`, retained in SMT when profiling with
Boogie `/emitDebugInformation:1`. Existing triggers, declaration order,
pruning, optimizations and solver settings are unchanged. The option is checked
before constructing any new AST nodes. No modulo law, inverse family,
injectivity fact or arithmetic-operation axiom is added.

## Validation

The unchanged original reproducer is `git-issues/Inputs/github-issue-33-original.dfy`.
The integration driver `git-issues/github-issue-33.dfy` checks both resolvers,
widths, conversion shapes, negative fit checks and a live false-proof control.
The review workflow runs that driver with pinned Z3 5.1.0 (`%review-z3`); the issue
validation workflow records Z3 5.1.0 measurements and actual Boogie/SMT inputs.
Its baseline is the green shipped build at `1d45201a027d46be24988fe0946c0bde80a702c9`.
The retained public artifacts contain per-VC results and commands.

### Solver boundary and stress limits

The prescribed composite trigger proves all original goals with Z3 5.1.0.
With Z3 4.12.1, the direct 32/64-bit round trips are cheap, but the unchanged
`ShiftRightByZero` still exhausts 20,000,000 resource units. Its actual solver
input retains both the new axiom and the old wrapper bridge. This older-solver
limitation is recorded rather than widening the trigger or changing solver
settings. The existing library/regression gates still use their established
solver configurations.

The generated scaling corpus verifies nesting through depth 64 and widths
through 1024. A single assertion batch combining 1000 distinct round trips
exhausts 20,000,000 units; this family does not guarantee linear total solver
cost. The negative guard mutations check explicit instances of the extracted
formula, while SAT witnesses give the wrapper its standard unsigned native
interpretation. Live Dafny false-proof controls separately audit actual matching.
