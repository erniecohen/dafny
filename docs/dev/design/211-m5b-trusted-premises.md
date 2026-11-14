# M5b for https://github.com/erniecohen/dafny/issues/211: a recorded trust list for standard-library premises

**Status:** approved and implemented in the same PR (https://github.com/erniecohen/dafny/pull/353).
The owner decisions of section 8 are recorded there; section 12 lists where the implementation
differs from the first draft of this design.

**Target:** `review/4.11.0`, inspected at `c9210a81d37c301482a77a7cf02bc1d085fcd6ac`, the merge of
M5a (https://github.com/erniecohen/dafny/pull/347). The design extends milestone 5 ("Std
migration") of
[`211-m4b-library-openness.md`](211-m4b-library-openness.md), section 13. It implements the
"valid explicitly recorded assumptions" that section 5.4 and section 7.1 of that design already
allow for and that M3–M5a deliberately left out.

**Decision:** add an opt-in, fixed and reviewed **trust list** of named ghost premises of the
standard library. A library built with `--modular-termination` may contain a premise only when the
premise is on the list and its statement and context match the list's digests. Its record names
every such premise. A client admits a library that has premises only when the client opted in to
the same list. `dafny audit` and the verifier's summary report every admitted premise. When nobody
opts in, nothing changes: records stay byte for byte what M5a writes, and with
`--modular-termination` off nothing changes at all.

## 1. Problem

Under `--modular-termination`, a library is admitted only as a `.doo` whose record shows that every
verification task of its stored program was Valid, with no unchecked premise
(`ModularTerminationRecords.AssumptionIn` and `UncheckedPremise` in
`Source/DafnyCore/ModularTerminationRecord.cs`). Every `{:axiom}`, `@Axiom`, `assume`, bodyless
concrete member and `{:extern}` refuses the record. M5a used that rule to certify the 31 premise-free
files of `src/Std`. The rest of Std carries premises that are true but that Dafny cannot prove:

| Module | Premises | Modules that are blocked only through it |
|---|---|---|
| `Std.Axioms` (https://github.com/erniecohen/dafny/pull/346) | 7 bodyless `{:axiom}` lemmas: `FunctionExtensionality.{Nullary, OnInt, OnReal, OnBool, OnOrdinal, OnIntInt}`, `Reals.Complete` | `RealCompleteness` |
| `Std.Ordinal` | 8 bodyless `{:axiom}` lemmas (`Succ`, `PlusDefinition`, `PlusStrictlyIncreasingOnRight`, `PlusIncreasingOnLeft`, `PlusIsAssociative`, `TimesStrictlyIncreasingOnRight`, `TimesIncreasingOnLeft`, `TimesDistributesOnLeft`) and 3 bodyless `{:axiom}` ghost functions (`Omega`, `PlusLimit`, `TimesLimit`) | `Termination`, `Frames`, and through them `Actions` |
| `Std.Strings` | the `@Axiom` lemma `HexConversion.CharsConsistent` | `Parsers`, `UnicodeEncodingForm`, `Utf8EncodingForm`, `Utf16EncodingForm`, `Utf8EncodingScheme` |

Two kinds of premise are **not** in scope, and stay refused:

- `assume {:axiom}` statements. These are `UnicodeStringsWithUnicodeChar.CharIsUnicodeScalarValue`,
  and the two `assume {:axiom} false` in `JSON/Deserializer.dfy` and
  `JSON/ZeroCopy/Serializer.dfy` (the JSON copy of `CharsConsistent` goes with them). A statement
  assumption is an unchecked hole in a body, not a stated fact, and `assume false` cannot be trusted
  at all. These need proofs, or for the first one a lemma in `Std.Axioms` that the body calls.
- `{:extern}` members of `TargetSpecific`, whose premise is the correctness of foreign code.

The coordinator's direction (relayed in https://github.com/erniecohen/dafny/issues/211) is to admit
exactly `Std.Axioms` plus upstream's existing `{:axiom}` members, listed explicitly, through a
recorded trust list that the `.doo` and the verifier's summary report.

## 2. What the trust list contains

The trust list is a table compiled into Dafny: a product source file reviewed like code, so it is
also covered by the fork-build identity that every record carries. Each entry is one declaration:

| Field | Meaning |
|---|---|
| `id` | the declaration's fully qualified name, such as `Std.Axioms.Reals.Complete` |
| `kind` | `lemma` (a bodyless lemma) or `ghost function` (a bodyless ghost function or predicate) |
| `statement_sha256` | digest of the resolved declaration as the stored-program printer prints it: attributes, signature, type parameters, `requires`, `ensures`, `reads` and `decreases` |
| `context_sha256` | digest of the declaration's **context**, defined below |
| `reason` | one line, with a link, saying why the premise is true in Dafny's intended model and sound against its encoding |

The **context** of a declaration is its enclosing module, plus every module that this module
imports or refines, taken transitively. Its digest is the SHA-256 of the lines
`<module key>\t<contract digest>`, sorted by key, over the M3 termination contracts of those modules
(a module's declarations, with the bodies of methods and lemmas left out; function bodies stay). These modules may come
from this library or from libraries it loaded. Binding to the context matters because a premise's
meaning depends on definitions near it. `Reals.Complete` is about the predicate `UpperBound`
defined beside it. `CharsConsistent` is about the constants of `HexConversion` and of the module it
refines. `PlusLimit` mentions `Plus`. If someone edits any of these, the reviewed entry no longer
describes the premise, so the list fails closed until the entry is reviewed again.

**Version 1 of the list, `standard-library`, has 19 entries:**

- the 7 lemmas of `Std.Axioms`;
- the 8 lemmas and 3 ghost functions of `Std.Ordinal`;
- `Std.Strings.HexConversion.CharsConsistent`.

Nothing else is on it. In particular:

- no member is listed by module or by pattern; a new lemma added to `Std.Axioms` is refused until
  its entry is added;
- no compiled declaration may be listed: every entry is a ghost declaration without a body, so a
  premise never contributes executable code (section 5);
- no statement assumption, `{:extern}`, `{:verify false}`, `@AssumeCrossModuleTermination`,
  `{:termination false}` used as an assumption, or `decreases *` may be listed. Those refusals of
  M3–M5a are unchanged.

`dafny audit` under the option prints, for each `{:axiom}` finding, whether the list admits it, and
for a listed declaration whose digests differ it prints both computed digests, which are what a
reviewer copies into the table. A regression test audits the standard-library project of section 7,
so that any edit to a premise or its context fails the test until the table and its review are
updated. Version 1 offers no way for users to supply
their own list. Section 8 lists that as an owner decision.

## 3. The option

Add `--modular-termination-trust` with the values `none` (the default) and `standard-library`.
It has no effect without `--modular-termination`. It is opt-in and, by the owner's decision, hidden
from the default help while the feature is experimental; its help text says that results become
conditional on the listed premises. It does not change any verification condition, so it is **not** one of the exact-match
semantic options in `ModularTerminationRecords.SemanticOptionList`. Admission handles it with the
subset rule of section 4.3, so a premise-free record such as the M5a core stays loadable whatever
the client chooses.

## 4. How the list is bound into the record

### 4.1 Producer

With `--modular-termination-trust:standard-library`, the producer refusal in
`AssumptionIn`/`UncheckedPremise` admits exactly the following:

1. a premise that the auditor or `UncheckedPremise` reports as `{:axiom}`/`@Axiom` (the auditor's
   `HasAxiomAttribute`, and the `{:axiom}` case of `UncheckedPremise`);
2. on a ghost lemma or ghost function without a body;
3. whose `id`, `kind`, `statement_sha256` and `context_sha256` match an entry of the selected list,
   computed both on the source program and on the **stored** program (the program the record
   certifies, after the round trip that the producer already checks); the two premise lists must be
   equal.

Any other premise refuses the record as today. The error names the declaration, and for a listed
`id` whose digests differ it says which digest changed. A bodyless `{:axiom}` declaration is found
twice today, once by the auditor's inventory and once by `UncheckedPremise`; the same match discharges
both.

### 4.2 Record

A record with at least one premise:

- uses `schema = 2`;
- adds `trust_list = "standard-library"`, `trust_list_sha256` (the digest of the whole table of that
  version) and `premises_sha256`;
- adds one table per premise:

```toml
[[modular_termination.premises]]
id = "Std.Axioms.Reals.Complete"
kind = "lemma"
statement_sha256 = "<64 hex>"
context_sha256 = "<64 hex>"
```

Premises are sorted by `id`. `RecordSha256` gains the lines `trust_list`, `trust_list_sha256` and
one `premise` line per entry, after the import lines. So a record cannot drop or add a premise
without failing its own digest. A record **without** premises keeps `schema = 1`, writes none of
these fields, and its canonical text and digest are byte-identical to what M5a writes. A Dafny that
predates this design fails closed on schema 2: `DooFile.AdmitModularTermination` already rejects an
unknown schema.

### 4.3 Consumer admission

`DooFile.AdmitModularTermination` accepts `schema` 1 or 2. For schema 2 it requires:

- that the client runs with `--modular-termination-trust` set to the record's `trust_list`. If not,
  the error names the option and the first premise: "it relies on the trusted premise
  `Std.Ordinal.PlusLimit`; use --modular-termination-trust:standard-library to admit it";
- that every recorded premise matches an entry of the client's table on all four fields. A table of
  a later Dafny build would anyway fail the existing fork-build check first;
- that `premises_sha256` and `trust_list_sha256` match;
- the context digests are not recomputed: the record digest already ties the premise list to
  `program_sha256`, which admission checks against the loaded program, so a library whose program and
  premise list disagree was modified after it was built and is refused by the existing checks.

Premises of a library's own dependencies are recorded by those libraries. The client loads every
library of a composition, so the premises of a composition are the union of the premises of its
admitted records.

## 5. Why admitting exactly the listed premises keeps the certification argument

The claim of the M4b design (section 7.1) is total correctness and termination of every complete,
accepted composition "under ordinary Dafny premises: sound base logic/ordinary termination rules,
valid explicitly recorded assumptions, and correct compiler/runtime/extern correspondence". M5a
used it only with an empty set of recorded assumptions. M5b makes that set non-empty, exact and
visible. The argument needs five facts.

1. **The hypothesis set is exact.** A producer issues a record only when every premise of its
   stored program is on the list with matching digests (4.1). The record names each one under its
   own digest (4.2). A client admits it only when it opted in to the same list and the names and
   digests match (4.3). So at every client the theorem's hypotheses are exactly the union of the
   recorded premises of the admitted libraries: nothing is hidden, and nothing is assumed that is
   not named.
2. **Premises contribute no code, so the termination argument is unchanged.** Every entry is a
   ghost declaration without a body. It is erased from compiled code, has no call graph of its own,
   and cannot return to a General or open chain. A call to a bodyless lemma is a leaf. A bodyless
   ghost function's `decreases` clause only orders the well-formedness checks of its own
   specification, and those checks still run. For `PlusLimit` that means its calls to `Plus` in its
   `ensures` are checked against `decreases right, 0`. The M4b invariant of section 7.1 is still
   established by checked obligations:
   - public contracts independent of private bodies;
   - openness treated as open;
   - entry bounds carried;
   - closed computations;
   - bounds justified in a closed stratum.

   A premise can be used in proving those obligations, so they hold *if the premises are true*,
   which is exactly the conditional form that section 7.1 states.
3. **No cross-module termination premise can enter through the list.** The list never contains
   `{:termination false}` as an assumption, `@AssumeCrossModuleTermination`, `decreases *`, a
   statement `assume`, an extern, or a member with a body. The refusals that M3 and M4a rely on all
   stay.
4. **The logical reach of each premise is known.**
   - *Lemma premises* are procedures. Their conclusions enter a proof only at an explicit call, for
     the arguments of that call; a program that never calls one, directly or through a verified
     wrapper, does not depend on it.
   - *Ghost-function premises* (`Omega`, `PlusLimit`, `TimesLimit`) are different. Their
     postconditions become axioms that hold wherever the function is in the verifier's context. So
     their premise is the existence statement ∀ arguments ∃ result satisfying the postconditions.
     This is a consistency premise, and if it is false it affects every proof that sees the
     function, not just callers.

   The record reports the kind of each premise so that a reader can tell these apart. Section 8 asks
   the owner whether to admit the three function premises or to restate them first as lemmas
   in `Std.Axioms`.
5. **Truth of each premise is a reviewed, recorded judgment.**
   https://github.com/erniecohen/dafny/issues/34 shows that a natural-looking premise (generic
   function extensionality) proves `false` against Dafny's encoding. So the list carries a reason
   and a review for every entry, and the context digest keeps the review attached to the text it
   reviewed. A false premise makes the conditional theorem vacuous for the programs that depend on
   it. The list makes that risk explicit, reviewable and attributable instead of silent.

A premise-free composition keeps M5a's unconditional claim: its records are schema 1 with no
premises, and its summary is unchanged (section 6).

## 6. Reporting

**Verifier summary.** When a run, whether a `verify` or a library `build`, admits or issues at least
one premise, `VerifyCommand` writes one more line after the existing trailer, `Dafny program
verifier finished with N verified, 0 errors`:

    Under --modular-termination these results assume 19 trusted premises of the standard-library trust list, from 2 libraries; dafny audit lists them

It names the count, the list and the number of libraries. A run with no premise writes nothing
more, so every existing expectation is unchanged.

**`dafny audit`.**
- *On a producer project.* Audit already lists every `{:axiom}`/`@Axiom` declaration. Under the
  option with the trust list selected, each listed premise keeps its finding and gains the note
  "admitted by the standard-library trust list"; unlisted findings say that they refuse the record.
  Audit never hides a premise.
- *On a client.* Under `--modular-termination`, audit adds a section with one finding per admitted
  library premise: the library file, the premise `id`, its kind, and the trust list. Without
  premises the section is absent and the output is unchanged.

**`.doo` inspection.** The premises are in `manifest.toml` (4.2), readable without Dafny.

**Documentation.** Add a paragraph to the library-admission text in
`docs/DafnyRef/Specifications.md` (next to the M5a sentence) and to the option's help text. The M5a
`README.md` gains a section on the trusted layer (section 7).

## 7. Standard-library layout

The premise-free core of M5a stays as it is, so that a client can still choose an unconditional
result. M5b adds a second project, `Source/DafnyStandardLibraries/modular-termination/trusted/`, the
**expanded core**: the core's files, unchanged, plus `Axioms`, `RealCompleteness` and `Ordinal`,
built as one library with `--modular-termination-trust:standard-library`. The M5a project
file is not touched, so the trust-dependent modules are added only where the trust option is set.
(The first draft layered this library on the core `.doo`; a single project is simpler and gives the
same records.) `Strings` is left out for now: its conversion modules refine
`Arithmetic.LittleEndianNat`, and the refined copies of the reveal lemmas of its opaque functions
carry `{:verify false}`, which the producer refuses; its entry stays on the list. The modules that
import these, `Termination`, `Frames`, `Parsers` and the Unicode
encoding modules, join in later steps once each builds.
`Actions` still waits for the bounds migration of section 9.2 of the M4b design. `JSON`,
`UnicodeStringsWithUnicodeChar` and `TargetSpecific` stay out (section 1). The `issue211-std.yml`
workflow builds both libraries and checks:

- the trusted layer's premise list against the table;
- a client of the trusted layer that verifies and prints the summary line;
- a client that is refused without `--modular-termination-trust`;
- a core-only client whose output is unchanged.

## 8. Owner decisions

Decided: (1) admit the three function premises in version 1, recorded with kind `ghost function` and
flagged as global premises by `dafny audit`; the restatement as lemmas is
https://github.com/erniecohen/dafny/issues/354. (2) Built-in list only. (3) The option name and values
below, default `none`, hidden from the default help.

1. **Admit the three Ordinal function premises?** The alternative is to restate `Omega`,
   `PlusLimit` and `TimesLimit` in `Std.Axioms` as lemmas that return a value with the stated
   properties, and to rewrite their uses. That keeps every listed premise local (5.4), at the cost of
   changing `Ordinal` and its importers. Recommendation: admit them in version 1, marked
   `ghost function` in the record, and open a follow-up for the restatement.
2. **Only a built-in list in version 1?** A user-supplied list (a file named by the option) would
   let other libraries record their own premises. It needs a story for who reviews such a list and
   how a client names the lists it accepts. Recommendation: built-in only, revisit after M6.
3. **Option name and values:** `--modular-termination-trust:none|standard-library`.

## 9. Option-off and opt-out identity

With `--modular-termination` off, none of this code runs: no option, record field, audit section or
summary line is reachable. With the option on and `--modular-termination-trust:none`, every
existing producer refusal, record byte, admission error and output line is unchanged. A regression
builds a premise-free library without the option, with `none` and with the list, and checks that
every `.doo` entry is byte for byte the same and that the record digest is the one M5a computes. No `BoogieGenerator` change is
involved, so verification conditions and resource counts are unchanged in all modes.

## 10. Implementation plan

One PR (the decision put both steps in this one):

1. **Trust list, producer, record, admission and reporting.** A new
   `Source/DafnyCore/ModularTerminationTrust.cs` holding the table and the statement and context
   digests. Changes in `ModularTerminationRecord.cs` (`AssumptionIn`, `RecordSha256`, schema 2) and
   in `DooFile.cs` (`AdmitModularTermination`). The option goes in `CommonOptionBag.cs`; reporting in
   `VerifyCommand.cs` and the auditor. Lit tests built from the real `src/Std/Axioms.dfy` and
   `RealCompleteness.dfy`:
   - a producer accepted with the list and refused without it;
   - an edited premise copy refused with the changed digest named;
   - an unlisted `{:axiom}` refused;
   - a client admitted with the option and refused without it;
   - the summary line and the audit section;
   - the M5a manifest unchanged with `none`;
   - a schema-1 record still admitted.
   - Also: the table regression of section 2, a release note, and docs and help.
2. **The trusted Std layer** of section 7, its README section, and the workflow extension.

## 11. What this does not change

- the bounded-callables design of https://github.com/erniecohen/dafny/issues/266;
- the orders work of https://github.com/erniecohen/dafny/issues/245;
- the Actions bounds migration (M4b design section 9.2);
- the milestone 6 release gates.

## 12. Implementation notes

- The option is hidden and has no effect without `--modular-termination` (section 3).
- The statement digest prints the resolved declaration (section 2), and the context is taken at
  the granularity of the M3 contracts.
- The consumer does not recompute context digests (section 4.3).
- The standard-library layout is one expanded project rather than a layer (section 7).
- There is no separate maintenance command; `dafny audit` prints the computed digests (section 2).
- Negative controls in `git-issues/github-issue-211-m5b.dfy`: an edited premise statement, an edited
  context, an unlisted `{:axiom}`, an `assume` statement and a client that did not opt in are each
  refused, and a premise-free library is byte for byte the same with `none`, with the list and
  without the option, with the record digest of M5a.
