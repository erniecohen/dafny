# The certified standard-library core under `--modular-termination`

Under the opt-in option `--modular-termination`
([issue 211](https://github.com/erniecohen/dafny/issues/211)), Dafny admits a library only as a
`.doo` file whose termination record shows that its own program was completely verified, with no
unchecked premise.  The bundled standard library carries no record, so it is rejected under the
option.  This directory is milestone M5a of the design
[`docs/dev/design/211-m4b-library-openness.md`](../../../docs/dev/design/211-m4b-library-openness.md)
(section 13, "Std migration"): the part of `src/Std` that can be certified as it is, without
changing a line of it.

## Building it

From this directory, with a build of Dafny from this tree and Z3 5.1.0:

    dafny build --target:lib dfyconfig.toml --output:DafnyStandardLibraries-modular-termination.doo

or `make build-modular-termination` in `Source/DafnyStandardLibraries`.  The build verifies the
core twice (the second time from the program stored in the library), and writes the library only
when every verification task of both runs succeeds.  The record ties the library to the build of
Dafny, the prelude and the Z3 binary that verified it, so the library is rebuilt for each build of
Dafny; it is not shipped as a binary.

## Using it

Load the library with `--library` and leave the bundled standard library out
(`--standard-libraries:false`, the default).  A client must use the same values of the options that
change what a proof means, which the record lists; for this core they are those of
`src/Std/dfyconfig.toml`:

    dafny verify --modular-termination --library:DafnyStandardLibraries-modular-termination.doo \
      --type-system-refresh --general-traits=datatype --general-newtypes \
      --reads-clauses-on-methods --enforce-determinism program.dfy

## What is in the core, and what is left out

The core is every module of `src/Std` (outside `TargetSpecific`) except the ones below.  Each is left
out because it, or a module it imports, has an unchecked premise, which a recorded library may not
have:

| Left out | Premise |
|---|---|
| `Ordinal` | eleven `{:axiom}` declarations about ordinal arithmetic |
| `Termination`, `Frames`, `Actions/*` | import `Ordinal` |
| `Axioms` | `{:axiom}` lemmas, by design: the standard library's collected trusted premises |
| `RealCompleteness` | imports `Axioms` |
| `Strings` | the `@Axiom` lemma `HexConversion.CharsConsistent` |
| `Parsers/*`, `Unicode/UnicodeEncodingForm`, `Unicode/Utf8EncodingForm`, `Unicode/Utf16EncodingForm`, `Unicode/Utf8EncodingScheme` | import `Strings` |
| `Unicode/UnicodeStrings` | an abstract interface for the two `--unicode-char` refinements; under the option a client cannot refine a library module, and the refinement in `src/Std` is left out |
| `Unicode/UnicodeStringsWithUnicodeChar` | `assume {:axiom}` in `CharIsUnicodeScalarValue`, and imports `Strings` |
| `JSON/*` | `assume {:axiom} false` in `Deserializer` and in `ZeroCopy.Serializer`, and imports `Strings` |
| `TargetSpecific/*` | `{:extern}` members, built per target language |

The list in `dfyconfig.toml` is explicit: a module added to `src/Std` is not certified until it is
reviewed and listed there.  The regression `git-issues/github-issue-211-m5a.dfy` checks that
`dafny audit` finds nothing in the core, and the workflow `issue211-std.yml` builds the core with
Z3 5.1.0 and loads it into clients.

Admitting the remaining modules needs either proofs of their premises or the recorded list of
trusted premises described next; `Actions` also needs the migration of its termination bounds that
the design's section 9.2 describes.

## The expanded core, with the standard-library trust list

Milestone M5b ([design](../../../docs/dev/design/211-m5b-trusted-premises.md)) adds a project,
`trusted/dfyconfig.toml`, that builds the core together with `Axioms`, `RealCompleteness`,
`Ordinal` and `Strings`, whose only unchecked premises are the 18 entries of the standard-library trust list
compiled into Dafny (`Source/DafnyCore/ModularTerminationTrust.cs`):

| Module | Trusted premises |
|---|---|
| `Axioms` | the six `FunctionExtensionality` lemmas, `Ordinals.Infinite`, `Ordinals.Replacement` and `Reals.Complete` |
| `Ordinal` | eight lemmas |
| `Strings` | `HexConversion.CharsConsistent` |

Each entry names a declaration and holds a digest of its statement and a digest of its context, the
contracts of the modules it can see; editing either one makes the entry no longer match, and the build
refuses it.  Any other premise is refused as before.

`Strings`'s conversion modules refine the abstract module `Arithmetic.LittleEndianNat`.  The copies
that refinement makes of the reveal lemmas Dafny generates for that module's opaque functions are
recognised as generated, so their `{:verify false}` is not refused as a user's
(https://github.com/erniecohen/dafny/issues/357).  The modules that import `Strings` stay out for
now.  The four modules have no premise but the listed ones; `Ordinal` and `RealCompleteness` import
only `Axioms`, and `Strings` imports only modules of the core.  Every premise is a lemma, so
its conclusions hold only where it is called; `Omega`, `PlusLimit` and `TimesLimit` are ghost functions
with proved postconditions, exported without their bodies, defined from the two `Ordinals` lemmas
(https://github.com/erniecohen/dafny/issues/354).

    dafny build --target:lib trusted/dfyconfig.toml --output:DafnyStandardLibraries-trusted.doo

The project sets `--modular-termination-trust:standard-library`, and a client of the library must
set it too, or the library is refused; the client's verification summary then says how many trusted
premises its results assume, and `dafny audit` lists each of them.  Without the option a client
admits only premise-free libraries, and the core built from `dfyconfig.toml` is unchanged.


## Checked Actions and complete common source (M5)

`overlay/build-source.py` materializes a separate, reviewed source projection. The
legacy source and its binary remain the option-off library. The checked projection
removes the twenty cross-module termination exemptions and checks public bounds.
Constructors establish immutable ghost allowances; fresh-producing operations prove
each result allowance strictly below an immutable ceiling. Virtual metrics still
check local termination. The five stored callback fields carry `bounded` evidence.
The three proof protocols have pure validity and allowance-bounded getters.

The projection also replaces the Unicode character-conversion assumption with its
proof, proves the JSON serializer by-methods, and repairs hexadecimal conversion's
canonical alphabet: sixteen digits, with uppercase aliases in the conversion map.
The checked JSON bound proof uses that sixteen-digit base. These changes do not
add axioms. Existing ghost premises remain conditional on the separately selected
M5b list, whose checked-source entries bind exact statements and contexts.

Build with the normal complete producer, from `Source/DafnyStandardLibraries`:

```
make build-checked-standard-libraries Z3=path/to/z3
```

This verifies both the original and retained source before issuing each common or
six-target certificate. Never use `--hidden-no-verify` for a checked artifact.
Generated checked binaries are ignored: each is bound to the exact verifier and
solver that produced it. Rebuild the pipeline after generation to embed them.
`--standard-libraries` with modular checking selects this distinct catalog; if the
catalog has not been built, the error gives the build instruction. An existing
common certificate from a different overlay recipe cannot be reused by the target
builder. The retained source records the recipe digest.

Target certificates additionally require the explicit
`--modular-termination-runtime-trust:standard-library` opt-in, empty by default.
Every assumed runtime contract, including termination and effects, is named in
`Source/DafnyCore/ModularTerminationRuntimeContracts.tsv`, bound to its statement
and context, retained in the certificate, and printed in verification/audit output.
It is separate from `--modular-termination-trust:standard-library`, which selects
ghost premises. Neither list approves unnamed assumptions or changed contracts.
See [the trust boundary](../../../docs/dev/design/211-m5b-trusted-premises.md).

`github-issue-211-checked-std.dfy` exercises the five field families, a
producer-of-producers client and hexadecimal conversion under both resolvers.
Each positive has a reachable-false companion. Hiding function bodies immediately
before the false assertion keeps that control focused on consistency rather than
unrelated unfolding. Certificate admission, target bundles and backend erasure
are additional release gates; source-only verification is not their substitute.
