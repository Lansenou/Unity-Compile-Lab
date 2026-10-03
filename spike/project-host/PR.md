# spike: per-project player test host

Draft research sources and measurements for a private 6000.3 project with about
6000 EditMode tests. The input project is unchanged and no private case names,
paths, packages, logs, DLLs or player binaries are included.

**Verdict: viable with graphics enabled under the sampled median gates.**
Not viable with -nographics under the parity gate.

| Measurement | Graphics | Nographics |
| --- | ---: | ---: |
| Sample runnable / selected | 164/200 | 164/200 |
| Sample pass / fail / skip | 151 / 12 / 1 | 102 / 62 / 0 |
| Sample oracle parity | 152/164, 92.7% | 102/164, 62.2% |
| Median boot to first test | 2.974 s | 3.893 s |
| Median total | 13.586 s | 9.123 s |
| Full cohort runnable | 1485/1829 | 1485/1829 |
| Full pass / fail / skip | 1361 / 117 / 7 | 889 / 580 / 16 |
| Full oracle parity | 1368/1485, 92.1% | 893/1485, 60.1% |
| Full boot / total | 5.546 / 340.675 s | 2.382 / 79.247 s |
| Crashes | 0 | 0 |

Fresh cold host build: 464.216 s, plus 44.251 s staging. Matching-key reuse:
23.354 ms with a precomputed key; fresh key calculation: 10.158 s.
The cold-built host reproduced the same sample outcomes. Real UTF coroutines:
24/24 passed, one additional case excluded.

Best coverage with the unmodified whole-file exclusions is 1485/1829 (81.2%).
Measured graphics-enabled same-outcome coverage is 1368/1829 (74.8%).
The full graphics boot misses the five-second target, so the sampled median
does not guarantee a sub-five-second boot. Main limits: graphics state accounts
for 475 recovered parity cases, compilation excludes 344 cases, and
Application.dataPath source-file checks fail in 89 cases.

The tool copies the project, hashes settings/assets/packages/bootstrap inputs,
builds a Mono development host, compiles external test DLLs against player
references, and runs UTF's own fixture/coroutine/log-scope pieces. It relies on
internal UTF APIs and is Windows/Mono-only. No supported ucl CLI contract or
production routing changes.

Validation: source compiler and focused reproduction checks passed.
scripts/check.sh reproduced the baseline define-manifest failure: 309 integration
tests passed, one failed; format/build/checksum, 636 core and 75 discovery tests
passed. The gate is not fully green. See spike/project-host/SPIKE.md for complete
per-bucket counts, method, exclusions and timing limits.
