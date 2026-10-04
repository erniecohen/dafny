// Compilation commands must reject B3 before looking for a worker or invoking Boogie.
// RUN: %exits-with 1 %baredafny build "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" > "%t"
// RUN: %exits-with 1 %baredafny run "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" >> "%t"
// RUN: %exits-with 1 %baredafny test "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" >> "%t"
// RUN: %exits-with 1 %baredafny build "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" --no-verify >> "%t"
// RUN: %exits-with 1 %baredafny run "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" --no-verify >> "%t"
// RUN: %exits-with 1 %baredafny test "%s" --verification-backend b3 --b3-worker "%t.missing-worker.dll" --no-verify >> "%t"
// RUN: %diff "%s.expect" "%t"

method Main() { }
method {:test} Positive() { assert true; }
