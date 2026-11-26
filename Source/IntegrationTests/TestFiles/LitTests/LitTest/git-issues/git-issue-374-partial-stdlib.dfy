// NONUNIFORM: retains the standard-library project options and declaration resource limits.
// RUN: %baredafny verify "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" --solver-path "%review-z3" --additional-axioms --cores 2 --verification-time-limit 0 --boogie /deterministicLiteralHashes:1 --boogie /restartProver --filter-symbol Std.Arithmetic.Power2.LemmaPow2MaskDiv2. > "%t"
// RUN: %baredafny verify "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" --solver-path "%review-z3" --additional-axioms --cores 2 --verification-time-limit 0 --boogie /deterministicLiteralHashes:1 --boogie /restartProver --filter-symbol Std.Producers.ConcatenatedProducer.Invoke. >> "%t"
// RUN: %diff "%s.expect" "%t"
