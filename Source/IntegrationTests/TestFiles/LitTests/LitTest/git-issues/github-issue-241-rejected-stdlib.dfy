// RUN: %exits-with 2 %baredafnyAtRepositoryRoot verify "Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" --type-system-refresh:false --additional-axioms:false --consistent-obligation-checks:false --filter-symbol Std.Base64.EncodeBVIsBase64. --cores 1 --verification-time-limit 0 > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/pull/263#issuecomment-6829656631
// Keep the reported command's complete shipped transcript, including its 19
// errors and exit code. No opt-in option is set. Core tests separately exercise
// the internal worker path without passing through CLI option validation.
