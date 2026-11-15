// RUN: %resolve --modular-termination "%repositoryRoot/Source/DafnyStandardLibraries/modular-termination/dfyconfig.toml" > "%t"
// RUN: %audit --modular-termination "%repositoryRoot/Source/DafnyStandardLibraries/modular-termination/dfyconfig.toml" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The certified standard-library core under --modular-termination, milestone M5a of
// https://github.com/erniecohen/dafny/issues/211.  The core, the project
// Source/DafnyStandardLibraries/modular-termination, resolves under the option, and 'dafny audit'
// finds no assumption in it: no {:axiom}, @Axiom, assume or {:extern} member, which a library with
// a termination record may not have.  The workflow issue211-std.yml builds the core as a recorded
// .doo with Z3 5.1.0 and loads it into clients; github-issue-211-m3-4.dfy checks the error that
// points a client of the bundled standard library at the core.
