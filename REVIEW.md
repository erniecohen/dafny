# review/4.11.0: Dafny 4.11.0 with soundness fixes

This branch is Dafny 4.11.0 (tag `v4.11.0`, commit `fcb2042`) with fixes for
eight soundness issues filed against dafny-lang/dafny, one commit per fix, and
it builds against Boogie 3.5.5 with a fix for a ninth, filed against
boogie-org/boogie.  Since `4.11.0+fcb2042d.review.256a1ab3` it also has fixes
for twelve issues filed here: one more soundness fix, crash and non-termination
fixes, a diagnostic fix and an auditor fix, one pull request each (below).
A build of it reports the version `4.11.0+fcb2042d.review.d2070b81`, and the tag
of the same name with a `v` marks it: `v4.11.0+fcb2042d.review.d2070b81`.

It is the shipped line.  Improvements go to the branch `dev`, which follows
upstream's `master`, and come back here only after they pass the tests.

Where the fork is going is in [`ROADMAP.md`](ROADMAP.md); the work itself is in
[the issues](https://github.com/erniecohen/dafny/issues).  How to work here, for
anyone, human or AI, is in [`AGENTS.md`](AGENTS.md).

## The fixes

| fix | test and release note | issue | what it changes | kind |
|---|---|---|---|---|
| `b6116c9` | (upstream's, in the fix) | [#6366](https://github.com/dafny-lang/dafny/issues/6366), fixed upstream by [#6367](https://github.com/dafny-lang/dafny/pull/6367) after 4.11.0 | An automatic induction hypothesis, and the range of a forall call statement, assume their call permissions only under the bound variables' type antecedent.  Upstream's commit, cherry-picked with its author. | weakens only |
| `cf5fdba`, refined by `5fc27c0` | `b7231f8`, `f102115` | [#6531](https://github.com/dafny-lang/dafny/issues/6531) | The equality axiom of a datatype with a single constructor gets the antecedent `Ctor?(a) && Ctor?(b)` in its direction "equal fields imply `Dt#Equal`", which ranged over every datatype value.  The other direction, "`Dt#Equal` implies equal fields", holds of every value, since `Dt#Equal` is equality, and stays unguarded.  `cf5fdba` guarded both directions; `5fc27c0` guards only the false one, as the pull request [dafny-lang/dafny#6540](https://github.com/dafny-lang/dafny/pull/6540) does, because guarding both made some proofs that compare tuples hundreds of times dearer. | weakens only: it still only adds a guard, now to one direction of the axiom |
| `f621876` | `251fbd1` | [#6537](https://github.com/dafny-lang/dafny/issues/6537) | Membership in `Map#Items` and `IMap#Items` gets a first conjunct: the item is a pair. | **asserts** that every item is a pair |
| `007b409` | `9e7df43` | [#6533](https://github.com/dafny-lang/dafny/issues/6533) | The assumption after a forall statement that assigns to the heap puts the bound variables' type antecedent outermost, around the call facts of its range. | weakens only |
| `5ecb3b4` | `c0713af` | [#6532](https://github.com/dafny-lang/dafny/issues/6532) | `BplForallTrim` keeps a bound variable whose type may be empty, under an `exists` guard, instead of dropping it.  Also substitutes the type arguments in the call facts of a constant field's right-hand side, which the first change exposed. | weakens only |
| `2ce2b7a` | `ca79a16` | [#6534](https://github.com/dafny-lang/dafny/issues/6534) | Deletes the unguarded `$Box($Unbox(x): T) == x`. | **asserts** `$IsBox`-guarded inverses for `ORDINAL` and `Field`, and `$IsBox` of the value a field holds in the heap |
| `b0f99c7` | `d67ac3e` | [#6535](https://github.com/dafny-lang/dafny/issues/6535) | `Map#Elements(Map#Glue(a, b, t)) == b` becomes pointwise inside the domain `a`; the same for `IMap#Glue`. | weakens only |
| `7512bd5` | `0861414` | [#6536](https://github.com/dafny-lang/dafny/issues/6536) | The guard of the ORDINAL `(o - m) + n` axiom, `n <= ORD#Offset(o) + m`, becomes `m <= ORD#Offset(o)`. | **asserts**: the axiom now also applies where it is true and did not before (`m <= Offset(o) < n - m`) |
| `a48933e` | `3ff7e36` | [boogie-org/boogie#1168](https://github.com/boogie-org/boogie/issues/1168) | Dafny builds against Boogie `3.5.5-review.37e4435d`, Boogie 3.5.5 with the fix (below).  Under the arguments type encoding, which Dafny uses, Boogie no longer emits the reverse casts `B_2_U(U_2_B(x)) == x`, no longer retypes a quantifier's `int` or `bool` variables to `U` for its triggers, and passes a value of a built-in type to a `U` position as a cast. | weakens only: it removes axioms, and the cast rewrites a term to one of the same value |

Issue numbers are dafny-lang/dafny's, except boogie-org/boogie#1168.  "Weakens
only" means that the fix removes an axiom or an assumption, or puts a guard on
one.  "Asserts" means that it also states a fact that was not stated before;
those parts are the ones a reviewer should check.  Each commit message says
what its fix changes and why.

## The fixes since 256a1ab3

Each is one squash-merged pull request here, with its regression test
`git-issues/github-issue-<issue>.dfy` and its release note
`docs/dev/news/<issue>.fix` in the same commit.  Issue numbers are this
repository's.  A crash, non-termination or diagnostic fix changes only programs
that crashed, hung or were diagnosed; it changes no verdict of a program that
already worked, as `AGENTS.md` requires.

| fix | issue | what it changes | kind |
|---|---|---|---|
| `36811f3` (#52) | [#40](https://github.com/erniecohen/dafny/issues/40) | Cloning a such-that assignment with missing bounds inside a `match` case no longer crashes the resolver. | crash |
| `8ef1431` (#53) | [#41](https://github.com/erniecohen/dafny/issues/41) | Allocating an object with a named constructor inside a `match` case no longer crashes the resolver. | crash |
| `d7b2b63` (#54) | [#42](https://github.com/erniecohen/dafny/issues/42) | Ghost inference covers locals initialized by `decreases to` and `nonincreases to` expressions, which crashed the resolver. | crash |
| `da1d421` (#55) | [#35](https://github.com/erniecohen/dafny/issues/35) | The auditor no longer reports bodyless instance members of traits as missing-body assumptions; static members and explicit assumptions are still audited. | auditor output |
| `64e5544` (#56) | [#46](https://github.com/erniecohen/dafny/issues/46) | Selecting a static member of a bitvector-based newtype under `--type-system-refresh` and `--general-newtypes` no longer crashes the resolver. | crash |
| `3ab6f38` (#57) | [#37](https://github.com/erniecohen/dafny/issues/37) | Type errors involving ambiguous arrow types show arrow syntax instead of internal type names. | diagnostic text |
| `cc10f9a` (#58) | [#43](https://github.com/erniecohen/dafny/issues/43) | An undeclared destination of a `:-` statement is reported instead of crashing, in both resolvers. | crash |
| `d24ac25` (#59) | [#45](https://github.com/erniecohen/dafny/issues/45) | The refreshed resolver resolves a datatype's signature before matching a qualified constructor's arguments, which crashed. | crash |
| `b796eff` (#60) | [#25](https://github.com/erniecohen/dafny/issues/25) | Refining a module with a `:|` whose constraint calls a function no longer crashes: the substituted trigger keeps its parsed form and heap label. | crash |
| `7959732` (#61) | [#39](https://github.com/erniecohen/dafny/issues/39) | The receiver of a static call written through an object, as in `(assert false; c).f()`, is checked inside a `match`, a quantifier, a set or map comprehension and a lambda, as it is elsewhere; cloning and substitution dropped it. | **soundness**, weakens only: it restores well-formedness checks and asserts nothing |
| `9af9f84` (#62) | [#44](https://github.com/erniecohen/dafny/issues/44), program (a) | A cyclic type synonym used by a later datatype or codatatype is reported instead of making the resolver run forever. | non-termination |
| `b97a6fe` (#64) | [#26](https://github.com/erniecohen/dafny/issues/26) | The existence check of a `:|` that binds many variables no longer overflows the stack: a long disjunction is balanced, and at most 20,000 partial guesses are kept. | crash; weakens only, since a dropped disjunct only strengthens the check |

The fix for #39 moves one verdict of the suite, `dafny2/SnapshotableTrees.dfy`,
from 102 to 103 verified with the same errors: a `reveal` inside a `match` case
now has its receiver checked, which adds one proved assertion batch.  No other
fix moves a verdict of the suite or of the standard library.  #63 moved a test
input under `Inputs/` and changed no product file.

## Boogie

Boogie's unguarded reverse casts,
[boogie-org/boogie#1168](https://github.com/boogie-org/boogie/issues/1168), are
fixed in Boogie.  The public fork [erniecohen/boogie](https://github.com/erniecohen/boogie)
carries the fix on its branch `review/3.5.5`, which is v3.5.5 with one fix commit
(its message says what changes and why the result is sound), its tests, and CI
that runs Boogie's own test jobs and packs Boogie's NuGet packages at the version
`3.5.5-review.37e4435d`.  The release `v3.5.5+review.37e4435d` carries them, with
their sha256 sums, and its `REVIEW.md` describes the fix and the version.  The
same fix is offered to Boogie's master as
[boogie-org/boogie#1169](https://github.com/boogie-org/boogie/pull/1169).

`a48933e` makes this branch build against those packages:

- `Source/DafnyCore/DafnyCore.csproj` names `Boogie.ExecutionEngine`
  `3.5.5-review.37e4435d`, and `dotnet-tools.json` names the `boogie` tool at the
  same version;
- `nuget.config` adds a local package source, `Binaries/boogie-packages`, and maps
  every Boogie package to it and to nothing else, so a missing package stops the
  restore instead of falling back to nuget.org's 3.5.5;
- `Scripts/fetch-boogie-packages.sh` downloads the 14 packages from the release
  into that folder and checks each against `Scripts/boogie-packages.sha256`, which
  is the release's `SHA256SUMS`.

**To build this branch, run `sh Scripts/fetch-boogie-packages.sh` once first.**
CI does so in every job that builds.

## Tests, release notes and the manual

Each fix after the first has a follow-up commit, as in upstream's pull
requests:

- a regression test, `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-<issue>.dfy`
  with its `.expect`, named as upstream's own fix for #6366 named its test;
- a release-note fragment, `docs/dev/news/<issue>.fix`, as `docs/dev/README.md`
  describes.

For boogie-org/boogie#1168, `3ff7e36` adds the test
`git-issues/boogie-issue-1168.dfy` and the release note
`docs/dev/news/boogie-1168.fix`, named by description, since the issue number is
Boogie's.

For #6531, #6532, #6533 and #6535 the test is the issue's program, which proves
`false` under 4.11.0 and is refused now.  For #6534, #6536 and #6537 no program
is known to prove `false`: each issue shows that the axioms themselves have no
model.  Their tests check what the fix changes that a program can see: for
#6537 and #6536, facts that 4.11.0 does not prove and that now verify; for
#6534, proofs about the values of fields that the deleted axiom used to carry
and that the fix keeps, and a vacuity control: a method in which the facts the
fix asserts are in play, and whose `assert false` must still fail.  For
boogie-org/boogie#1168 no Dafny program is known to prove `false` either, and the
test checks proofs about values read from arrays, sequences, maps, fields and
sets, which relied on the reverse casts: they verify under 4.11.0 and with the
fix, and fail when the reverse casts are dropped without the fix's casts.
Boogie's own tests for the fix, on its branch `review/3.5.5`, include the issue's
program, on which Boogie 3.5.5 proves `assert false`.

Three of upstream's tests record the verifier's resource counts in their
expected output (`dafny0/CoinductiveProofs.dfy`, `dafny0/SubsetTypes.dfy`,
`dafny1/SchorrWaite.dfy`).  The fixes for #6534 and #6536 move those counts,
though not any verification result, and `7b6dba7` updates the expected outputs.
The Boogie fix moves those counts again, and `74581b2` updates them the same way.
Otherwise upstream's own harness gives the same outcome with and without the
eight Dafny fixes for every test under `dafny0` to `dafny4`, `git-issues` and
`logger` (run with the C# backend only).

The Boogie fix changes how much work some proofs take.  Upstream's harness was
run for it on the tests named in `.github/review/regressions` only, not on every
test.  There it cost one proof in `dafny4/GHC-MergeSort.dfy`, which was already
dear, and `cf5c56e` repairs it with one assertion, as upstream's own fix for #6366
added hints to that file.  In the suite, at its resource limit, it moves the
verdicts of a few programs, none by a new error: `15d1b11` updates the expected
verdicts, and its message says why each one moved.

The refinement of the fix for #6531, `5fc27c0`, moves no verdict of the suite
and none of the resource counts those three tests record, and `git-issue-6531`
is still refused.

None of the fixes changes the Reference Manual or the command-line help.  They
change the verifier's encoding, not the language or its options, and where the
manual speaks to the point (`m.Items` is a set of pairs; `o - m` is defined only
when the offset of `o` is at least `m`) it already says what the fix makes the
verifier know.  Each follow-up commit gives its fix's reason.

## The version, and how to check it

`d2070b81` is the first 8 hex digits of the sha256 of the change that the
product commits make to files that v4.11.0 already has:

    git diff --diff-filter=M --abbrev=7 v4.11.0 b97a6fe | sha256sum

`b97a6fe`, the fix for #26, is the last product commit.  `.github/review/base`
names `ada0d09`, the same product tree on that pull request's branch before it
was squash-merged; the two give the same hash.  Before the twelve fixes since,
the version was `256a1ab3`, as follows.

`256a1ab3` is the first 8 hex digits of the sha256 of the change that the
product commits make to files that v4.11.0 already has:

    git diff --diff-filter=M --abbrev=7 v4.11.0 5fc27c0 | sha256sum

`5fc27c0` is the last product commit, the last commit that changes a file
the build compiles; here it is the refinement of the fix for #6531, which came
after `a48933e`, the commit that moves Dafny to the fixed Boogie.  Before them,
the line was `4.11.0+fcb2042d.review.c47a78b3`, whose last product commit was
`7512bd5`, the last of the eight fix commits; that build was first named by
applying the eight fixes to a fresh repository of v4.11.0's tree with
`git apply` and hashing `git diff`.
That diff leaves out the files the fixes add, which stay untracked, and
abbreviates object ids to 7 digits; the command above computes the same thing
from a clone of this repository that has the tag `v4.11.0`.  (In a very large
clone git may lengthen an abbreviation that would be ambiguous; a shallow fetch
avoids that.)

**The version names the product, the source that is compiled.**  The commits
after `b97a6fe` add or update tests, release notes, documents and CI.  They
change no product file, so the version stays.  The rule on this branch:

- a commit that changes or adds a product file is a new last product commit,
  and gets a new version and a new tag;
- any other commit keeps them.

The product is everything outside `Source/IntegrationTests/`, `docs/`,
`.github/`, this file, and the other documents at the top of the tree:
`ROADMAP.md`, `AGENTS.md` and `CLAUDE.md`.  `.github/review/base` names the base
and the last product commit, and CI's `.github/review/version.sh` computes the
version from them and fails if a later commit changes a product file.  By hand,
this prints nothing:

    git diff --name-only b97a6fe <commit> -- . ':!Source/IntegrationTests' ':!docs' ':!.github' \
      ':!REVIEW.md' ':!ROADMAP.md' ':!AGENTS.md' ':!CLAUDE.md'

A release binary built from the tag prints the version with `dafny --version`.
It is built with `-p:SourceRevisionId=fcb2042d.review.d2070b81`.  The version
names the Boogie packages by their version, and `Scripts/boogie-packages.sha256`
names their bytes.

## CI

`.github/workflows/review.yml` replaces upstream's workflows on this branch and
on `dev`, which exercise every compiler backend.  On this branch it fetches the
Boogie packages, builds Dafny, runs the programs of Dafny's own test suite that
verify (under `LitTest/dafny0` to `dafny4` and `git-issues`) at a resource limit
of 16,000,000 with Z3 5.1.0, taken from Z3's public GitHub release by checksum,
and reports each program's verdict.  It verifies Dafny's standard library,
`Source/DafnyStandardLibraries`, as the library's Makefile's `verify` target does,
once with Z3 5.1.0 and once with the Z3 that the tree's own tests expect (4.12.1 on
this branch), and reports each declaration's verdict.  It also runs the tests named in `.github/review/regressions` in
upstream's own harness.  On a tag it attaches self-contained binaries for `osx-arm64`
and `linux-x64` to a GitHub release, with their sha256 sums.

### Expected verdicts

`.github/review/expected-verdicts.tsv` holds the verdict the suite expects for
each program: its exit code, the verifier's summary line (proofs verified,
errors, proofs out of resource) and the lines with errors.  The job `Verdicts`
fails when any verdict changes, or a program is added or removed.  A program's
time and the resources its finished proofs use are not part of its verdict, so
a change in them alone does not fail it; a proof that stops finishing within
the resource limit does.

To change the file on purpose:

1. Push the change.  The job `Verdicts` fails and lists each program whose
   verdict moved, with the expected and the new verdict.
2. Take `expected-verdicts.tsv` from that run's artifact `verdicts-off` (or write it
   with `python3 .github/review/lit-verdicts.py expected verdicts.tsv`), and
   replace `.github/review/expected-verdicts.tsv` with it.
3. Commit it on its own.  The commit message must say, for each program whose
   verdict changed, why: which change made a proof finish or stop finishing,
   or that the program itself changed.  A verdict is never updated without a
   reason on record.

### Expected standard-library verdicts

`.github/review/expected-std-verdicts-z3-5.1.0.tsv` and
`expected-std-verdicts-z3-4.12.1.tsv` hold the verdict the job `Standard library`
expects for each of the library's declarations under that Z3: its outcome
(`Correct`, `Errors`, `OutOfResource`, ...), and for each part of the run (the
library, and its target-specific files for each target language) the exit code,
the summary line and the lines with errors.  They are updated as the suite's
expected verdicts are, from the run's artifact `std-verdicts-off-z3-<version>`, with a
reason for every row that changes.

The library runs with its own resource limits and without a time limit, in
Boogie's default declaration order, as upstream verifies it.  Dafny does not
verify the library again when a program uses it (`--standard-libraries` loads its
prebuilt binary), so these verdicts say whether the library's proofs still go
through on this line, not whether a program that uses the library verifies.  Not
all of them do, and most of those that do not are Z3 5.1.0's: unmodified 4.11.0
verifies every declaration with Z3 4.12.1, and leaves 29 unverified with Z3 5.1.0,
27 of them because a proof runs out of its resource limit.

That order is not quite deterministic: the resource counts of some declarations
move a little from one run to the next (`.github/review/std-verdicts.py` gives the
figures).  So a proof close to its limit could change its verdict with no cause.
If one does, run the job again before updating the file.


### Additional axioms enabled

The suite and source-library gates also run with `--additional-axioms`, using
separate expected verdicts and recording paired OFF/ON resource counts. See
[the standing axiom CI documentation](docs/dev/additional-axioms.md#standing-ci-for-the-shared-option-50)
for scope, retained negative/vacuity controls, resource-order caveats and the
scratch-only baseline-capture procedure. The OFF verdict files remain unchanged.
