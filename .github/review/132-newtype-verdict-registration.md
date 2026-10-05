# New reference-characteristics regression rows

The [first complete isolated follow-up gate](https://github.com/erniecohen/dafny/actions/runs/37245006317) at `0239c5ae96289981cfd9eeca08ec78deb3ddacb3` found exactly these 16 missing inputs in each suite table. Every old suite row and all four standard-library matrices remained equal. These tables record each new input first RUN, which explicitly selects additional axioms off. Secondary axiom-enabled harness coverage is a separate required check. No old verdict is replaced.

| New input | Expected exit and summary | Reason |
|---|---|---|
| `git-issue-132-newtype-finite-actual-positive-triggered.dfy` | 0 / 2 verified, 0 errors | New existing-language finite actual positive triggered regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-general-arrow-characteristic-negative.dfy` | 2 | New existing-language general arrow characteristic negative regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-hidden-characteristic-negative.dfy` | 2 | New existing-language hidden characteristic negative regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-mixed-subset-cycle-negative.dfy` | 2 | New existing-language mixed subset cycle negative regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-partial-total-positive.dfy` | 0 / 3 verified, 0 errors | New existing-language partial total positive regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-partial-total-universal-proof-nonvacuity.dfy` | 4 / 1 verified, 3 errors | The inhabited final false assertion and two deliberately false old-allocation postconditions still fail. |
| `git-issue-132-newtype-partial-total-universal-proof.dfy` | 0 / 7 verified, 0 errors | Existing reference-free allocation applies universally to all six raw and wrapped partial and total carriers. |
| `git-issue-132-newtype-provided-characteristic-positive.dfy` | 0 / 0 verified, 0 errors | New existing-language provided characteristic positive regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-provided-inhabited-false-negative.dfy` | 4 / 0 verified, 1 error | New existing-language provided inhabited false negative regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-provided-newtype-no-characteristic-negative.dfy` | 2 | New existing-language provided newtype no characteristic negative regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-provided-no-characteristic-negative.dfy` | 2 | New existing-language provided no characteristic negative regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-reference-characteristic-negative.dfy` | 2 | New existing-language reference characteristic negative regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-revealed-control-positive.dfy` | 0 / 0 verified, 0 errors | New existing-language revealed control positive regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-sequence-allocation-negative.dfy` | 4 / 2 verified, 1 error | Newly captured references do not make the wrapped function sequence allocated in the earlier heap. |
| `git-issue-132-newtype-sequence-allocation-preallocated.dfy` | 0 / 3 verified, 0 errors | New existing-language sequence allocation preallocated regression, with the intended first-RUN outcome recorded. |
| `git-issue-132-newtype-sequence-allocation-pure.dfy` | 0 / 3 verified, 0 errors | New existing-language sequence allocation pure regression, with the intended first-RUN outcome recorded. |
