# Friction cases

Small, self-contained Dafny programs that show where Dafny makes a proof much
harder than its mathematics warrants, or where getting past a blocker needs a
trick that is not obvious.  They are kept here, on this fork's line, and not
offered upstream.

There are two kinds:

1. **Friction to fix** (status `open`, `filed`, `fixed`).  A small proof that
   is much harder than it should be: it fails, costs far more resource units
   than an equivalent form, or needs hints that should not be needed.  An
   `open` case is a candidate for a fix in this fork; a `filed` one names the
   fork issue; a `fixed` one names the release that fixed it, and from then on
   it is a regression check: its check block is rewritten to what the fix
   makes true.
2. **Tips** (status `tip`).  A non-obvious blocker whose current workaround is
   accepted as it is: no fix is wanted.  The case records the workaround, and
   its check confirms that the pattern still works.

Every case is minimal and scrubbed: it names no private project, machine or
path.  Where the friction only shows in a larger development, the case says
so, describes the context in words, and keeps the isolated form.

## Layout

```
friction/
  README.md          this file
  run                the runner
  dfyconfig.toml     shared options: resource limit 5,000,000 per declaration,
                     no time limit, /normalizeDeclarationOrder:0
  cases/NNN-slug/
    case.md          the write-up, with its check block
    repro.dfy        the reproducer, self-contained, no includes
    workaround.dfy   the workaround, when it is code (optional)
    dfyconfig.toml   the case's options; base = "../../dfyconfig.toml",
                     includes = ["repro.dfy"]
```

### case.md

In this order:

- A title, `# NNN: what goes wrong`, and a list with
  - **Status**: `open`, `filed` (with the fork issue), `fixed` (with the fork
    release), or `tip`;
  - **Seen on**: the Dafny release and Z3 version the case was observed on;
  - **Options**: the options beyond the shared ones.
- **Reproducer**: what `repro.dfy` shows, usually the program itself.
- **Expected**: what should happen.
- **Observed**: what happens: error text, outcomes, resource units.
- **Workaround**: how to get the proof through today (`workaround.dfy`, if it
  is code).
- **Suggested fix**: in Dafny or Boogie, or "none wanted" for a tip.
- **Source**: where the friction came from, in general terms only
  ("a downstream development: ...").
- **Check**: the machine-readable check block (below).
- **Last recheck**: the date, Dafny release, Z3 version and outcome of the
  latest run.

RU means Z3 resource units as Dafny reports them for a declaration, summed
over its verification tasks.  Resource units, not elapsed time, are the
measure of proof cost here: for a given Dafny release, Z3 version and options
they are a property of the program.

### The check block

A fenced block with the language `check` lists what the reproducer is
recorded to show, one fact per line (`#` starts a comment):

    exit N                  Dafny's exit code (0 ok, 2 parse or resolution error, 4 verification error)
    message "TEXT"          the output contains TEXT
    absent "TEXT"           the output does not contain TEXT
    verified NAME           every verification task of NAME is correct
    failed NAME             some task of NAME has errors
    failed-any NAME ...     at least one of the NAMEs failed
    outofresource NAME      some task of NAME ran out of resource, none has errors
    ru NAME >= N            NAME's resource units, summed over its tasks
    ru NAME <= N
    ratio NAME1 NAME2 >= K  RU(NAME1) / RU(NAME2) >= K
    workaround-exit N       the workaround's exit code (default 0)
    context-only            the friction needs a larger context than the
                            reproducer; the lines record the isolated behaviour

NAME is the declaration as Dafny names it, without the `(correctness)` or
`(well-formedness)` suffix, for example `Indexed` or `OneTrigger.Common`.

Record the facts that make the friction visible, and as few figures as
possible: prefer outcomes and ratios to absolute resource counts, and give a
count a generous bound, since resource counts move between Dafny and Z3
versions.

## Running

    friction/run DAFNY [--solver-path Z3] [--cores N] [CASE-PREFIX ...]

`DAFNY` is the `dafny` binary to check (or a command line, such as
`"dotnet path/to/Dafny.dll"`).  `--solver-path` names the Z3 to use; give the
Z3 version the cases were seen on (5.1.0) to compare with their records.
Prefixes select cases: `friction/run ./dafny 003 007`.

For each case the runner verifies the case's `dfyconfig.toml`, and the
workaround when there is one, and reports:

- `REPRODUCES`: every check line holds; the friction is still there.
- `TIP-HOLDS`: the same, for a tip.
- `CONTEXT-ONLY`: the isolated reproducer behaves as recorded; the friction
  itself needs a larger context.
- `CHANGED`: some check line no longer holds, or the workaround's exit code
  is not the recorded one.  The friction is gone or looks different: read the
  misses it lists and update the case (for a release that fixes it, status
  `fixed`, with the release).

It also prints each declaration's outcome and resource units, and a
`RESULT {...}` JSON line per case.  It exits 0 when every case is as
recorded.

To check a candidate fix, build it (or take the binaries that `review.yml`
makes with `-f binaries=true`) and run every case with it: the cases it should
fix come out `CHANGED`, and every other case should not move.

## Adding a case

1. Take the next number and a short slug: `cases/NNN-short-slug/`.
2. Write `repro.dfy`, as small as still shows the friction, with no includes.
3. Copy `dfyconfig.toml` from a neighbouring case and keep only the options
   the reproducer needs.
4. Add `workaround.dfy` if the workaround is code.  It is verified under the
   same options and must verify (or set `workaround-exit N` in the check).
5. Write `case.md` as above, with its check block, run
   `friction/run DAFNY --solver-path Z3 NNN`, and record the outcome under
   **Last recheck**.

Keep everything in a case publishable: no machine names, local paths, job
identifiers or names of private projects, and no measurements other than
those of the reproducer itself on a stated Dafny release and Z3 version.
