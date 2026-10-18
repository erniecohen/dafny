# Heap-free lambda facts: a note for review

**Status: a draft on `scratch/claude/lambda-facts`.** It is opt-in, behind
`--heap-free-lambda-facts`, and it is not merged. The last product commit is
`6261a0028`, and the version is `4.11.0+fcb2042d.review.bcc0c421`. The full argument is
in [`docs/dev/additional-axioms.md`](../../docs/dev/additional-axioms.md), "Body
permissions of heap-free lambdas in every heap (own option)".

## What it changes

The option changes the translation only for a *selected* lambda: one with no reads
clause, whose requires and body use nothing that observes the heap or allocation.

- **It weakens one check.** The lambda's well-formedness check no longer assumes
  that the lambda's heap succeeds the current heap, nor that the formals are
  allocated. This only removes assumptions.
- **It asserts facts.** These are the part to check. The permissions (`F#canCall`)
  of the calls in the lambda's range and body are emitted under the formals' types
  and a good heap alone. They hold in every good heap, through #160's own quantifier
  and family equality. Its `ApplyN`, `ReadsN` and `RequiresN` patterns take bound
  type arguments, and #160's destructor pattern, which cannot mention those
  arguments, is omitted.

With the option off, the selection is false and nothing changes. CI shows 0 changed
verdicts in the suite and the standard library, in both modes of
`--additional-axioms`.

## Why the asserted facts are true

Each exported permission is an obligation that the weakened check proves: for
every good heap, and every value of the formals' types that satisfies the range, in
the formation context. That is the same justification as the existing lambda
permissions and as #36's permissions after assign-such-that. A permission says
nothing about allocation, a footprint, or set or map membership, so the
counterexamples of #82 and #132 are untouched:

- #132's original and negative programs still fail with the option, in both
  resolver modes.
- #82's lambda allocation control has a reads clause, so it is not selected.
- The negative program's vacuity controls fail with the facts in play.

## Opt-in, and its own option

It asserts facts, so `AGENTS.md` requires an option.

- It first joined the shared `--additional-axioms`. There, the regression test of
  issue 100 ran out of its 16,000,000-unit limit with that option on
  ([run 39127008194](https://github.com/erniecohen/dafny/actions/runs/39127008194)),
  so it now has its own option, as `AGENTS.md` provides for a measured cost.
- Whether it should instead be a default, as #137's pure-callback consequence
  became by the owner's exception, is the owner's decision.

## Evidence

- [Run 39129585777](https://github.com/erniecohen/dafny/actions/runs/39129585777) is
  green.
  - All the registered regression tests pass, including
    `git-issues/lambda-facts-every-heap.dfy`, `git-issue-82*.dfy`,
    `github-issue-132*.dfy` and `git-issue-100.dfy`.
  - Every suite and standard-library comparison, with the shared option off and on,
    has 0 differences.
- The suite and standard library do not run with `--heap-free-lambda-facts` on. An
  earlier form of this member was measured inside the shared option, with
  automatic triggers and without the type-general patterns
  ([run 39125339582](https://github.com/erniecohen/dafny/actions/runs/39125339582)):
  - 0 of 1,161 suite programs moved.
  - In the standard library, 8 rows moved with Z3 5.1.0 and 2 with Z3 4.12.1. These
    were out-of-resource tasks in the families that already vary (#30); the
    out-of-resource count went from 23 to 22, and from 1 to 0.

## Known limits

- **Facts arrive only where the family is applied.** A permission that a proof
  reaches only through another permission term is not supplied, for example after an
  assign-such-that whose constraint applies the lambda's callee directly.
- **Costs move.** More permissions reach the search, so some proofs get dearer and
  some cheaper.
- **The selection is conservative.** It excludes bodies with binders, nested lambdas,
  statement expressions and heap or allocation constructs.
