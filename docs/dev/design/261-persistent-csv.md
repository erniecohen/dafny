# CSV reporting during persistent module replay

This extends the opt-in persistent module cache from [PR 564](https://github.com/erniecohen/dafny/pull/564), part of [issue 261](https://github.com/erniecohen/dafny/issues/261).

`CSVTestLogger` consumes a task's current display name, outcome, duration, resource count, random seed and replay marker. All are already represented by the successful module manifest and its current symbol binding. Batches of one current symbol share a scope token, preserving the fresh logger's grouping and assertion-batch labels. It does not inspect assertion tokens, descriptions or proof dependencies. Therefore CSV-only reporting can use the same module replay as an unlogged run without extending the persisted schema or relaxing its identity checks.

Eligibility follows the logger parser's case-sensitive name boundary: every requested logger must be `csv` or begin `csv;`. Parameters still pass through the existing parser. JSON and text need assertion/proof metadata absent from replayed tasks; TRX has not been admitted to this reporting contract. A mixed logger request falls back as a whole. Other existing replay exclusions remain in effect. No behavior changes without `--persistent-translation`.

The equality contract excludes runtime and replay-marker fields. With the result cache enabled, CSV ordering is already canonical by display name; current names, outcomes, resource counts and seeds must equal an independently populated run without persistent translation. Cached durations are labeled `TimingReplayed=true`. The existing `--persistent-translation-audit` oracle independently checks stored translations and module results against freshly translated/prepared tasks. Modules containing a failed task still use the ordinary task-result path.

The regression compares cold and warm reports with an independent nonpersistent oracle, requires actual module hits and zero solver starts on warm runs, retains an unsoundness witness, and exercises audit, source rebinding, stale/corrupt entries and mixed logger fallback. Mutations that disable CSV replay or admit unsupported loggers must fail those checks.
