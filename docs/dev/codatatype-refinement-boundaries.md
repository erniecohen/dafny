# Co-recursive calls and type membership

A productive co-recursive call denotes a suspended value while Dafny checks the
fields of its defining constructor. Productivity establishes that successive
observations take finite work. It does not establish that an arbitrary suspended
payload satisfies a subset predicate, a newtype predicate, or a type parameter's
instantiated constraint.

For example, `type Impossible = x: int | false witness *` has no values. A
co-datatype with an `Impossible` payload cannot acquire a value merely because a
recursive call occurs under a constructor. Treating that call's declared result
type as already established makes the constructor's field check circular and
allows a subsequent client to prove `false`.

## Verification rule

The repair for [issue 143](https://github.com/erniecohen/dafny/issues/143) removes
the ordinary function callability assumption for suspended co-recursive calls.
That assumption previously activated the existing consequence axiom, including
the result's full type membership. It also removes the corresponding automatic
single-constructor query assumption. Extreme predicates and their prefix
predicates retain their separate proof rule.

Before an observation supplies ordinary type facts, the verifier checks the
existing membership predicate at the actual scoped type:

- A destructor must establish its observed result's type.
- An ordinary helper's input must establish its instantiated formal type.
- A match source must establish its type before constructor and pattern-variable
  facts enter a branch. Exact let bindings receive the same check before their
  generated bindings supply type facts.
- A substituted default containing a suspended call is checked again at the
  call site. Its declaration was checked under typed formal inputs, which the
  suspended actual argument has not established.

The checks adapt boxing to the existing representation and use type membership
without allocation facts. They assert obligations; they do not assume their
answers. The repair adds no axiom, restores no unchecked callability fact, and
introduces no option. Passing a suspended value directly into the productive
recursive constructor position remains allowed.

A verified helper alone cannot justify an exemption. Its verification assumes
its formal argument's type membership. Applying that proof to a suspended value
without establishing the premise recreates the circular reasoning.

## Compatibility limitation

These checks are conservative. In particular, the current encoding may lack the
finite-observation facts needed to prove a valid destructive observation of a
suspended value. The following valid patterns verified before the repair and are
now rejected by the registered compatibility controls:

- Taking a refined head from a productive self-call.
- Forwarding a tail observed from a productive self-call.
- Passing a productive self-call through an abstemious constructor helper.
- Destructuring a productive self-call with a match expression.

This is a completeness loss. It is required by the current proof boundary; it is
not evidence that those definitions lack mathematical values. Restoring the old
unchecked result facts would also restore the reproduced contradictions. A
more permissive rule needs a separate sound derivation of the finite observation
being used, without assuming the suspended value's full type membership.

The development repository comparison also records changed verdicts for existing
programs: `dafny3/Abstemious.dfy` (five errors in suspended helper and destructor
observations), `dafny3/WideTrees.dfy` (one coinductive postcondition),
`dafny3/Zip.dfy` (five coinductive postconditions), and `dafny4/Circ.dfy` (one
coinductive postcondition). Those programs previously verified. The latter proof
losses extend beyond constructor-body observations: suppressing the shared
callability collector also withholds automatic callability propagation when
co-recursive definitions are unfolded in later proofs. The repair does not claim
to retain every previously accepted coinductive proof. These outcomes are recorded
by [the development comparison](https://github.com/erniecohen/dafny/actions/runs/37228257735)
and the verifier-suite verdicts, rather than hidden by removing the programs.

Ordinary productive definitions whose immediate payloads are established retain
universal observations for natural-number, refined even-number, generic, mutual,
and multiple-constructor streams. Constant defaults and defaults observing an
already constructed head are also covered. The tests keep inhabited controls
separate from contradictory definitions: each clean control establishes a
concrete value before a final `assert false`, which must remain unprovable.

The public diagnostic comparison is recorded in
[run 37227420637](https://github.com/erniecohen/dafny/actions/runs/37227420637).
It compares development source and a 4.11.0 baseline with the repair on development
and the current 4.11.0 fork. These diagnostics cover both resolver modes with
checksum-checked Z3 5.1.0. They are evidence for the reproduced paths and the
compatibility controls, rather than a proof of the entire compiler.
