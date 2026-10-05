// Enabled commands propagate B3 and fail its unsupported configuration before target hooks.
// RUN: %exits-with 4 %baredafny build "%s" --target py --verification-backend b3 --arithmetic-solver 1 --b3-worker "%t.missing-worker.dll" --solver-path "%t.missing-solver" > "%t"
// RUN: %exits-with 4 %baredafny run "%s" --target py --verification-backend b3 --arithmetic-solver 1 --b3-worker "%t.missing-worker.dll" --solver-path "%t.missing-solver" >> "%t"
// RUN: %exits-with 4 %baredafny test "%S/Inputs/git-issue-126-tests.dfy" --target py --verification-backend b3 --arithmetic-solver 1 --b3-worker "%t.missing-worker.dll" --solver-path "%t.missing-solver" >> "%t"
// Ordinary skip compiles/executes without reading missing worker or solver files.
// RUN: %baredafny build "%s" --target py --verification-backend b3 --b3-worker "%t.missing-worker.dll" --solver-path "%t.missing-solver" --no-verify >> "%t"
// RUN: %baredafny run "%s" --target py --verification-backend b3 --b3-worker "%t.missing-worker.dll" --solver-path "%t.missing-solver" --no-verify >> "%t"
// RUN: %baredafny test "%S/Inputs/git-issue-126-tests.dfy" --target py --verification-backend b3 --b3-worker "%t.missing-worker.dll" --solver-path "%t.missing-solver" --no-verify >> "%t"
// The two translate capability failures remain unchanged.
// RUN: %exits-with 1 %baredafny translate cs "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" >> "%t"
// RUN: %exits-with 1 %baredafny translate cs "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" --no-verify >> "%t"
// RUN: %OutputCheck "%s.expect" < "%t"

method Main() { print "b3-skip-run\n"; }
