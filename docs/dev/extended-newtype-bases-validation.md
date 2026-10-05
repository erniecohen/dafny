# Extended newtype bases: public validation observations

These are completed public CI observations for issue #113. Registered verdict
agreement, focused proof success and compiler execution have different scopes.
This record does not establish that every repository proof succeeds, the full
definition of done is satisfied, or proof costs are unchanged.

## Strict canonical review

The [development review](https://github.com/erniecohen/dafny/actions/runs/37251525888)
ran at `62e9e1c2b71abf3fb284107b3e33484b55292981`, with compiled product
`7b063ecd6414a845631cd587196c683fd243c86a`. The
[shipping review](https://github.com/erniecohen/dafny/actions/runs/37256512608)
ran at `3ea78f81317ee205382dfc0d114de56cc2d3886f`, with compiled product
`95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6` (Source95) and version
`4.11.0+fcb2042d.review.27c95800`. Later regression-oracle, documentation and review-workflow changes preserve
the compiled product and its build-input manifest.

| Observed scope | Development | Shipping |
|---|---:|---:|
| Registered regression harness cases passed | 233 | 286 |
| Selected Core / language-server / integration cases passed | 268 / 8 / 4 | 267 / 8 / 4 |
| Canonical suite programs | 1,287 | 1,273 on each additional-axioms axis |
| Suite raw exits 0 / 1 / 2 / 4 | 592 / 16 / 290 / 389 | 581 / 16 / 293 / 383 on each axis |
| Suite differences from registered verdicts | 0 | 0 on both axes |

Shipping also passed 35 resolver checks, two cycle-edit checks and six inherited
member/edit checks, plus four literal-identity checks and one editor-option
invalidation check. The canonical regression harness selects C# compilation;
the five-backend evidence below has its own denominator.

Standard-library verdict comparisons also report zero differences from their
registered tables. Each matrix contains seven run rows as well as the
declaration rows shown below. Non-successful proof outcomes remain visible:

| Line / Z3 / additional axioms | Total rows | Correct declarations | Errors | OutOfResource |
|---|---:|---:|---:|---:|
| Development / 5.1.0 | 2,439 | 2,427 | 1 | 4 |
| Development / 4.16.0 | 2,439 | 2,427 | 1 | 4 |
| Shipping / 5.1.0 / off | 2,197 | 2,166 | 2 | 22 |
| Shipping / 5.1.0 / on | 2,197 | 2,165 | 2 | 23 |
| Shipping / 4.12.1 / off | 2,197 | 2,190 | 0 | 0 |
| Shipping / 4.12.1 / on | 2,197 | 2,190 | 0 | 0 |

The development main-library calls and both shipping 5.1.0 main-library calls
exit 4; the shipping 4.12.1 calls exit 0. All six target-specific calls in each
matrix exit 0. A passing registered-verdict comparison retains the expected
proof failures and resource limits; it is not an all-proofs-success result.

## Same-source runtime and proof observations

The [same-source run](https://github.com/erniecohen/dafny/actions/runs/37255927050)
uses Source95 and the version above. Its portable manifest covers 1,515 compiler
build inputs, with SHA-256
`9b14543faa7a11e7a076210007d8ffe60556ea020b6f003ebe843a0b94c3104f`.
Actual compiler, Boogie, bundled-library and solver identities were checked
before and after the observations.

The reviewed requested scope comprises 36 literal Compiler RUN lines in 16
source files: 12 feature runtime fixtures and four prerequisite controls. All
216 requested backend child processes exit 0: C# has 72 observations across its
two compilation modes, and JavaScript, Python, Go and Java have 36 each. All 16
literal harness cases pass, and their 36 outer Compiler RUN processes exit 0.

There are two separate verifier denominators. The unchanged compiler harness
makes 39 verifier calls using its default Z3 4.12.1 configuration; all exit 0.
The derived standalone projections make 78 calls using explicit Z3 5.1.0 and
both explicit additional-axioms axes. All 78 exit 0, with 446 recorded assertion
batches succeeding, no proof errors and no Dafny-source warnings. The projected
calls retain their source contracts and recorded options. They do not replace
the canonical harness calls or establish solver-query or resource equality.

The observer also records 108 children outside the five requested backends:
106 unsupported exits (36 C++, 36 Dafny and 34 Rust, all exit 3) and two Rust
exit-1 `NullReferenceException` failures in ORDINAL field compilation. Those
exceptions are retained; Rust success and a measured unchanged-baseline cause
are not claimed.

## Collector corrections and lifecycle inventory

The original observation report retains its false aggregate metadata/review
flags. Its proof collector incorrectly compared CSV `RandomSeed` 0 with an
omitted JSON property: the logging adapter supplies a default integer value.
The actual proof logs, exits, counts, emitted programs and component receipts
were reviewed separately. No actual seed is inferred from either representation.

The original filtered discovery calls returned a no-match placeholder for Core
and language-server tests. The
[fresh lifecycle run](https://github.com/erniecohen/dafny/actions/runs/37259205592)
uses unfiltered discovery followed by explicit selection from the pinned source
definitions, while preserving the executed filters and Source95 build identity.
Selected discovery names match the executed TRX names as multisets: 267 Core,
eight language-server and four integration cases, all passed. Multiplicity is
retained for repeated theory display names. Earlier raw reports are unchanged;
the corrected receipt is separate evidence.

These public receipts have received source and data review for the scopes
stated here. They do not by themselves certify every feature contract or the
remaining final acceptance work. The
[default-off compatibility report](extended-newtype-bases-default-off-compatibility.md)
records the separate paired verdict and emitted-program observations. The
[solver query-context investigation](extended-newtype-bases-query-session-cost.md)
retains unresolved proof-cost differences; exact default-off cost parity is not
claimed. The [source and VC review](extended-newtype-bases-review.md) retains the
known conservative proof losses and proof boundaries.

## Shipping composition after the finite-support repair

The current shipping base incorporates [the finite-support soundness repair](https://github.com/erniecohen/dafny/pull/160) at `854f5a65a50658c8c415629d67d0b1bd34860fed`. The composed product is `4e50e86cde6ab2ef6cab0762ffc4df59d646dc15`. Its source review preserves the guarded finite images and reads, the existing capture-allocation introduction, one previous-heap binder, and the suspended co-call boundary. The existing finite-support audit and map-substitution tests remain mandatory in its workflow.

Parent registrations were composed row by row, preserving each one-sided change. Three overlapping aggregate Std run rows are provisional pending an actual composed-source capture. The new integration's proof outcomes, resource counts, compiler packages, runtime/lifecycle and performance receipts remain unmeasured. The earlier pinned observations in this document certify their recorded source only; they do not certify this composition or satisfy its remaining default-off cost requirement.
