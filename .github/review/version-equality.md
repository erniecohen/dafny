# HEAD-derived version compatibility

The product pathspec and diff flags are unchanged. For each revision below,
compare the former calculation (the upstream tag to the product commit recorded
at that revision) with the new calculation (the upstream tag to that revision).
The first row is the base tip for this change; the other three are older release
commits. Every result also agrees with its recorded `v<version>` release tag.

| Commit | Former version | HEAD-derived version |
| --- | --- | --- |
| `93220f9c35fca564b34042126a14c7a5b0b13f5f` | `4.11.0+fcb2042d.review.e075a6ea` | `4.11.0+fcb2042d.review.e075a6ea` |
| `b831b6b8c00949e98c4d052d53a0185e4f81b59e` | `4.11.0+fcb2042d.review.687b2e7b` | `4.11.0+fcb2042d.review.687b2e7b` |
| `499e42cb658f72e7519e72f5fce020ab0fd97041` | `4.11.0+fcb2042d.review.59cabc4d` | `4.11.0+fcb2042d.review.59cabc4d` |
| `913cd84e99c1d336161909206f6eeea1c894d903` | `4.11.0+fcb2042d.review.fde322eb` | `4.11.0+fcb2042d.review.fde322eb` |

The current `.github/review/base` is deliberately unchanged: its first word is
the upstream tag, and its historical second word is optional and ignored.
Keeping the existing two-word file lets older tooling read it; new product
changes no longer update it.

`test_version.py` checks a stamped diff against the new calculation, committed
product edits/additions/deletions, excluded paths, ignored or absent historical
commits, uncommitted edits, the non-release fallback, and a depth-1 clone that
never fetches the stamped product commit. Release controls reject a failed or
empty derivation, preserve the version/tag equality check, and refuse existing
tags.

Validation: all 159 review bookkeeping tests pass. Eight sequential mutations
were detected: using the stamped endpoint, using the working tree, omitting
added product files, requiring the historical commit, including documents,
hiding a macOS release derivation failure, hiding a maintenance derivation
failure, and accepting an empty maintenance version. Each made its focused test
fail; the original source was restored after each mutation.
