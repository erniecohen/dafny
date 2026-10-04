# Working on this fork

These are the rules for anyone who works on erniecohen/dafny, human or AI.
Claude Code reads them through `CLAUDE.md`.  What the shipped line is, and how its
version is computed, is in [`REVIEW.md`](REVIEW.md).  Where the fork is going is in
[`ROADMAP.md`](ROADMAP.md).  Upstream's own practice, which this fork follows, is in
[`CONTRIBUTING.md`](CONTRIBUTING.md) and [`docs/dev/`](docs/dev/README.md).

## The branches

- **`review/4.11.0`**, the default branch, is the shipped line: Dafny 4.11.0 with
  the fixes, built against a fixed Boogie.  A change to a product file makes a new
  last product commit, which gets a new version and a new tag.  `REVIEW.md` defines
  the product, in "The version, and how to check it".
- **`dev`** follows upstream's `master`, with this fork's CI in place of upstream's
  workflows.  Improvements are written there.  They come to `review/4.11.0` only
  after they pass the tests.
- **`master`** is a copy of upstream's `master`.
- **`fix/<issue>-<slug>`** is the head of a pull request to upstream: one fix, on
  upstream's `master`.
- **`scratch/<name>/...`** is for CI experiments.  Delete a scratch branch when you
  are done with it.
- **Tags `v4.11.0+fcb2042d.review.<hash>`** mark the shipped line's versions, and
  each has a release with binaries.
- **Boogie's fix** lives in the fork [erniecohen/boogie](https://github.com/erniecohen/boogie),
  on `review/3.5.5`.  Its release packages are what this line builds against.

## Finding and taking work

- **The work list is [the issues](https://github.com/erniecohen/dafny/issues).**
  Their labels are listed in `ROADMAP.md`.
- **Claim an issue before you start.**  Assign it to yourself, or comment on it
  that you are taking it.  A session that pushes under someone else's account
  comments, and names itself by its session or branch name, never by a machine or
  a path.  Then add the label `in-progress`.
- **One holder per issue.**  If an issue is assigned, or claimed in a comment, ask
  there before you touch it.
- **Link your branch from the issue**, and later your pull request.  Report what
  you found and what you did there, including what failed.
- **File new work as an issue**, with its labels, after searching for one that
  exists.  Do not file an issue you have not reproduced.

## What every change needs

As `CONTRIBUTING.md` and `docs/dev/README.md` ask:

- **A release-note fragment** in `docs/dev/news/`, named `<issue>.fix` or
  `<issue>.feat` after the upstream issue, or `<description>.fix` when there is
  none.
- **A regression test** in the integration tests:
  `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-<issue>.dfy`,
  with its `.expect`.  **A crash fix has the crashing program as its test.**
- **Reference Manual (`docs/DafnyRef/`) and `--help` updates** when a documented rule
  or interface changes: a new option, a new message, a changed result.  A crash fix or
  an error-message fix needs none: the release-note fragment and the regression test
  describe it.  `--help` text describes options and commands, never individual fixes.
- **A commit message that says what changes, and why.**  For a soundness fix, say
  whether it only weakens (removes an axiom or an assumption, or guards one) or
  also asserts a new fact.  The parts that assert are the ones a reviewer must
  check (`REVIEW.md`, the fixes).

## What may change the defaults

A soundness fix may change what Dafny does under default options.  So may a crash
fix or an error-message fix: it changes only programs that crashed, or the text of a
diagnostic, never the verdict or the cost of a program that already worked.  Every
other change sits behind a new opt-in option, except that added axioms share one
(below).  With the option off, it must give the same verdicts and the
same resource counts as before.  Show that, on the suite and on the standard
library, before anything relies on the option.

## Merging fix pull requests

Every product change updates `.github/review/base` and appends its test to
`.github/review/regressions`, so two open fix pull requests always conflict there.
They are merged one at a time.  After one merges, the next is rebased onto
`review/4.11.0`, its `base` and `regressions` lines are redone, and its CI is run
again before it merges.

## New axioms are where soundness is lost

A missing axiom, such as one for bitvectors, is fixed by adding one, and one wrong
axiom makes everything provable.  Each new axiom needs:

- **a written soundness argument**: a model in which it holds, or a derivation from
  definitions that are already trusted;
- **a vacuity control**: a test in which `assert false` must still FAIL with the
  axiom present, in a context that triggers it;
- **to ship opt-in**, behind an option, because an extra axiom can move proof costs
  or start matching loops under default flags.  With the option off, Dafny gives
  the same verdicts and resource counts as a build without the axiom.

**One option for all added axioms.**  The axioms that supply missing facts, such as
those that [issue 33](https://github.com/erniecohen/dafny/issues/33) and
[issue 34](https://github.com/erniecohen/dafny/issues/34) report, all go behind one
command-line option that they share, not one option each.  It is off by default,
and a project file (`dfyconfig.toml`) can set it, like any other option.

- **The shared option is `--additional-axioms`**, default false, also available
  as `additional-axioms = true` in `[options]` of `dfyconfig.toml`. Its current
  family is the bounded integer/bitvector round trip, with local verification instances of
  existing native integer literal identities, and the reflexivity of heap succession on good
  heaps, described in
  [`docs/dev/additional-axioms.md`](docs/dev/additional-axioms.md).
- **Every later axiom joins that option.**  The option turns all its axioms on at
  once, so a later axiom's soundness argument covers it together with those
  already there.
- **An axiom whose measured performance effect is a problem gets its own option
  instead.**  The change that adds it says why, with the measurements.

**This option policy is provisional.**  It is expected to be revisited once it is
clearer where upstream Dafny is going.

A missing axiom is not automatically a bug fix under the upstream rule below.
Label its issue `needs-owner-approval` before any pull request upstream.

## The verdict gates

- **`.github/review/expected-verdicts.tsv`** holds the verdict of each program in
  the suite.  The job `Verdicts` fails when a verdict changes, or a program is
  added or removed.  A changed resource count alone does not fail it.
- **`.github/review/expected-std-verdicts-z3-<version>.tsv`** holds the standard
  library's verdicts, declaration by declaration, for the job `Standard library`.
- **To change either on purpose,** follow `REVIEW.md`, "Expected verdicts":
  1. Push, and let the job fail and list what moved.
  2. Take the new file from the run's artifact.
  3. Commit it on its own, with a reason for every verdict that moved.

  A verdict is never updated without a reason on record.

## Building, testing and pushing

- **Build:** run `sh Scripts/fetch-boogie-packages.sh` once, then build as upstream
  does.  The script fetches the fixed Boogie packages and checks their hashes.
- **Test in this repository's CI.**  It is free for a public repository.  The
  workflow `review.yml` runs on pushes to `review/**` and `dev`, on pull requests to
  them, on `v*` tags, and on demand.  To run it on a scratch branch, use
  `gh workflow run review.yml --ref scratch/<name>/...`.
- **Push to `review/**` or `dev` only a tree whose CI already ran green on a
  scratch branch.**  Then fast-forward.
- **Never rewrite history.**  GitHub rulesets forbid force-pushing and deleting on
  `review/**` and `dev`, and moving or deleting a `v*` tag.  A wrong commit gets a
  revert commit.  A wrong tag gets a new tag.
- **A failed run notifies the repository's owner.**  So a scratch or probe
  workflow that expects failures records them, in the job summary and an artifact,
  and exits 0.

## Upstream

- **Pull requests to dafny-lang/dafny go from the `dev` line.**  Cut a branch from
  upstream's `master`, which `dev` follows, without `dev`'s CI commits.  Name it
  `fix/<issue>-<slug>`, and put one fix in each pull request.
- **Only bug fixes go upstream** unless the owner approves otherwise.  Soundness
  fixes are bug fixes; missing axioms are not automatically (above).
- **Human maintainers approve.**  Disclose AI assistance, as `CONTRIBUTING.md` asks.
- **Replies to maintainers are drafted for the owner**, who decides what to post.
  A pull request may be updated with commits.
- **Naming an upstream issue or pull request shows up on its page upstream.**  That
  holds for `dafny-lang/dafny#<N>` in a commit message, an issue or a comment
  here.  Name one where it matters.

## This repository is public

Put nothing here that is specific to a contributor's machines or private
projects:

- no host names;
- no local paths;
- no names of private repositories;
- no job ids;
- no measurements taken on private machines.

That holds for files, commit messages, issues, comments, tags, releases and
workflows.  Cite public references only: issues, pull requests and files,
upstream or here.
