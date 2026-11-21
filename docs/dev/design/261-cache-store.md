# The shared cache store (issue 261)

One directory holds every persistent cache of Dafny: verification results, module resolutions, module
translations and Boogie preparation state. This note says where it is, how entries are named, published and
read, and how the large tier is bounded. The user-facing summary is in the User Guide, under "The shared cache
store".

## Root

The root is, in order of precedence:

1. `--verification-cache DIR` (or its alias `--cache-dir DIR`) on the command line;
2. `verification-cache = "DIR"` in `dfyconfig.toml` (project files use the option's own name);
3. the environment variable `DAFNY_CACHE_DIR`.

With none of them, nothing is cached and nothing is written. Every cache needs `--type-system-refresh`; a
root given on the command line or in a project file without it is an option error, and a root from the
environment alone is ignored.

## Layout

```
ROOT/v1/                       the small tier (verification results; see 261-content-addressed-caches.md)
  results/ module-results/ solver-identities/ image-digests/ blobs/
ROOT/v1/artifacts/             the large tier
  LAYOUT                       the layout version, "1"
  resolutions/KK/KEY           one entry per key; KK is the key's first two hex digits
  translations/KK/KEY
  preparations/KK/KEY
  partitions/P/holds/TABLE/KK/KEY
  partitions/P/last-run        the partition's last run
  partitions/P/lock
  tags/T/TABLE/KK/KEY
  sweep.lock  last-sweep
```

A key is a lowercase hex SHA-256 digest computed by the stage that owns the table. It depends only on content,
options, the Dafny release and solver bytes, never on a path, branch, project root or job, so two checkouts of
the same sources share entries. The resolution key is described in `261-resolution-identity.md`; the
classification of every option into the keys of each stage is `261-option-classes.tsv`.

## Option sensitivity

Each stage's key is default-deny over options: every option is an input unless the stage lists it as irrelevant,
with a reason. For resolution the list is `ModuleResolutionKey.IgnoredOptions`.
`DafnyDriver.Test/ResolutionKeyOptionTest` starts from every option of every command at its default and toggles
each in turn: the digest must change exactly for the options not listed, and `--progress` at any level must
leave it. That test cannot tell an option that is wrongly listed, so
`.github/review/resolution-cache-options.py` checks the listed options it can reach from the command line
semantically: with the cache warmed at defaults, a run with the option changed must reuse every module, and
`--resolution-cache-check` must find each reused module equal to a fresh resolution under that option. (Listing
`--warn-shadowing` as a mutation makes the check report a difference.) Options in the key, from the command
line, a project file, `--boogie`, and hidden and `--legacy-*` options, must reuse nothing.

## Entries

An entry is `"DART"`, a format number (1), the table and key as strings, the payload's length, the payload,
and a SHA-256 digest of everything before it. A reader checks all of it: a wrong table or key (an entry
copied to another name), a short file or a wrong digest makes the entry a miss, and the reader deletes it
so it can be stored again.

Publication writes a temporary file in the entry's directory, flushes it, and renames it into place. Renames
are atomic on local file systems and on NFS, so a reader sees a whole entry or none, and reading takes no lock.
Two writers racing on one key may both rename their copies; the key is a digest of the content, so the
second rename replaces the entry with the same bytes. A stage that finds an entry it cannot use (one written
by a build with a different decoder, say) republishes with `replace`, which renames over it.

## Partitions and holds

Every run accounts to a *partition*: `--cache-partition NAME`, the project file's `cache-partition`, the
environment variable `DAFNY_CACHE_PARTITION`, or by default `project-` and 16 hex digits of a digest of the
project root (the project file's directory, else the current directory). The partition is accounting only. It
is never part of a key, and a partition reads every partition's entries.

A run *holds* every entry it publishes or reads: `partitions/P/holds/TABLE/KK/KEY` records the entry's size,
and its modification time is the partition's last use of the entry (updated at most hourly by reads, always by
publication). Each holder is charged the entry's full size.

- **Budget.** At the end of a run that published something, the partition drops its least recently used holds
  until their total is within `--cache-partition-budget` (MB, default 10240). Dropping a hold deletes nothing
  by itself.
- **Deletion.** An entry is deleted only when nothing holds it: no partition and no tag. Collection deletes
  unheld entries older than ten minutes (younger ones may be in the middle of being published and held) and
  holds whose entry is gone.
- **Orphans.** A partition with no run for `--cache-orphan-days` days (default 30) is deleted with its holds.
- **Cap.** When the large tier exceeds `--cache-size-cap` (MB, default 102400), entries are deleted in order of
  last use by any holder, untagged entries before tagged ones, until it fits. The run logs which partitions lost
  entries and how many, on standard error, prefixed `cache:`.

Orphan expiry and the cap are checked by a sweep, at most once a day at the end of a run, and by
`dafny cache gc`. Each partition's budget and the sweep take a lock file (created exclusively, taken over
when older than ten minutes); a run that finds one held skips that step.

## Tags

`dafny cache keep --tag T` makes tag `T` hold exactly the entries the current partition holds, replacing what
it held before. Tagged entries outlive budgets and expiry, and the cap deletes them last. `dafny cache release
--tag T` drops the tag. A dedicated partition per branch (`--cache-partition main`) and `keep --tag main` after
each run of that branch keep the branch's working set.

## The small tier

Verification results and resource counts are small, are kept indefinitely and are shared machine-wide. They
are pruned only by removing `ROOT/v1` minus `ROOT/v1/artifacts`, or by a future release-scoped prune; nothing
in this note deletes them.

## Tests

`Source/DafnyCore.Test/ArtifactStoreTests.cs`: round trip and holds; two checkouts sharing an entry, and a
differing input missing; concurrent publication with readers never seeing a partial entry; damaged and
renamed entries; replacement; a shared entry outliving one holder's eviction; a churning partition leaving
another's entries alone; orphan expiry; the cap's order and log; tags; name and key validation.
`.github/review/resolution-cache.py` drives the resolution table through the CLI, including damaged and
corrupt entries; `.github/review/cache-requires-refresh.py` checks the option rules.
