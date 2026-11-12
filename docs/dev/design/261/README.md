# Issue 261 design artifacts

Read [the design](../261-content-addressed-caches.md).

These artifacts accompany a documentation/proposal PR against `review/4.11.0`.
The two C# patches are **not applied to product sources**. They apply to Dafny
`428e348b0b125cb67505f03a0026b3f497f98e48`, with Boogie source reviewed at
`45a8869bb43456f126577f5ba6fd24725ed6e4d9`.

## Run the models

Python 3.10 or newer, standard library only; no compiler, solver, network or
repository checkout is needed:

```sh
python3 models/run_models.py
```

There are 57 passing test methods: task keys 11, module DAG 14, naming 12,
storage 20. The models cover 400 seeded edit scenarios, 128 cold/warm task
configurations, 96 module/permutation comparisons and 128 concurrent model
translations. These are executable specifications and counterexamples, not
tests of the C# implementation. The storage model uses POSIX publication.

## Check proposal applicability

From a clean checkout of the Dafny pin, pass both files to `git apply --check`:

```sh
git apply --check path/to/history-free-translation.patch path/to/content-resolution.patch
```

That does not compile or install the proposals. Full input capture, the typed
snapshot codec, global projection, output carrier and option wiring remain
unimplemented. See [validation](VALIDATION.md).

## Option inventory

`261-option-audit.tsv` contains 761 conservative source occurrences, each linked
to its pinned source. It is not 761 distinct options or a runtime-exhaustiveness
proof. Regenerate it with:

```sh
python3 option_inventory_261.py path/to/dafny path/to/boogie output.tsv
```

The archive linked from the issue contains the complete documentation patch,
the two standalone C# proposal patches, models, option audit and this validation.
It contains no repository snapshot. Its documentation patch omits the archive
itself to avoid a recursive archive-in-patch dependency.
