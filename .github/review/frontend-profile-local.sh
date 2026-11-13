#!/bin/sh
# The front-end measurement protocol of .github/workflows/issue241-frontend-profile.yml, on one
# machine, for core-count scaling (https://github.com/erniecohen/dafny/issues/241, milestone D1).
# Same corpora, configurations, rounds and tables as the workflow; every job runs here in
# sequence, so all comparisons are paired on this machine.
#
#   .github/review/frontend-profile-local.sh [OUT]
#
# Environment: DAFNY (a built Dafny; default: build with `dotnet publish` into out/dafny),
# REPS (10), SEED (1), CORES (all logical CPUs: --cores of the no-op-solver runs and the
# runtime's view), JOBS (space-separated subset of: std-resolve core-resolve examples-resolve
# std-translate suite std-heap; default all).  Needs python3, and dotnet-gcdump on PATH for
# std-heap (`dotnet tool install --global dotnet-gcdump --version 10.0.750501`).
#
# The tables are in OUT/report.md, the raw CSVs and profiles under OUT.  Measurements taken on
# a private machine are not to be committed or posted here (AGENTS.md); report them where the
# owner decides.
set -eu
root=$(cd "$(dirname "$0")/../.." && pwd)
out=${1:-$root/frontend-results}
reps=${REPS:-10}
seed=${SEED:-1}
cores=${CORES:-$(python3 -c 'import os; print(os.cpu_count())')}
jobs=${JOBS:-std-resolve core-resolve examples-resolve std-translate suite std-heap}
script=$root/.github/review/frontend-profile.py
cd "$root"
if [ -z "${DAFNY:-}" ]; then
  sh Scripts/fetch-boogie-packages.sh
  dotnet publish Source/Dafny/Dafny.csproj -c Release -o out/dafny -p:GeneratePackageOnBuild=false
  DAFNY=$root/out/dafny/Dafny
fi
all="ws-default-off,ws-default-on,ws-default-gcev,srv-default-off,srv-default-on,srv-default-gcev"
std=Source/DafnyStandardLibraries/src/Std/dfyconfig.toml
mkdir -p "$out"
for job in $jobs; do
  case $job in
    std-resolve)
      python3 "$script" run --corpus std-resolve --command resolve --project "$std" --configs "$all" \
        --dafny "$DAFNY" --out "$out/$job" --reps "$reps" --seed "$seed" ;;
    core-resolve)
      python3 "$script" run --corpus core-resolve --command resolve --project Source/DafnyCore/dfyconfig.toml \
        --configs "$all" --dafny "$DAFNY" --out "$out/$job" --reps "$reps" --seed "$seed" ;;
    examples-resolve)
      python3 "$script" run --corpus examples-resolve --command resolve \
        --project Source/DafnyStandardLibraries/examples/dfyconfig.toml --configs "$all" \
        --dafny "$DAFNY" --out "$out/$job" --reps "$reps" --seed "$seed" ;;
    std-translate)
      python3 "$script" run --corpus std-translate --command verify --cores "$cores" --project "$std" \
        --configs ws-default-off,ws-default-on,srv-default-off,srv-default-on \
        --dafny "$DAFNY" --out "$out/$job" --reps "$reps" --seed "$seed" ;;
    suite)
      lit=Source/IntegrationTests/TestFiles/LitTests/LitTest
      python3 .github/review/lit-verdicts.py plan "$lit" "$out/plan.tsv"
      python3 "$script" run --corpus suite-0 --command verify --cores "$cores" --plan "$out/plan.tsv" --litdir "$lit" \
        --configs ws-legacy-off,ws-legacy-on,ws-refresh-off,ws-refresh-on \
        --dafny "$DAFNY" --out "$out/$job" --reps "$reps" --seed "$seed" ;;
    std-heap)
      python3 "$script" heap --corpus std --project "$std" --windows 1,4 --reps 3 --cores "$cores" \
        --dafny "$DAFNY" --out "$out/$job"
      python3 "$script" gcdump --corpus std --project "$std" --windows 1,4 --cores "$cores" \
        --points parsed,resolved,1,20,43,80 --dafny "$DAFNY" --out "$out/$job" ;;
    *) echo "unknown job $job" >&2; exit 2 ;;
  esac
done
python3 "$script" analyze "$out" --out "$out/report.md" > /dev/null
echo "tables: $out/report.md"
