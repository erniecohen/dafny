// RUN: python3 "%S/Inputs/github-issue-211-m3-4.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%t.dir" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" > "%t"
// RUN: %diff "%s.expect" "%t"

// Termination records in .doo libraries under --modular-termination (milestone M3-4 of
// https://github.com/erniecohen/dafny/issues/211).  The driver builds libraries, tampers with
// copies of them, and checks each admission and production control; it keeps every command and
// output in its output directory.
