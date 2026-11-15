// RUN: python3 "%S/Inputs/github-issue-358.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%t.dir" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%repositoryRoot/Binaries/z3/bin/z3-4.12.1" > "%t"
// RUN: %diff "%s.expect" "%t"

// Regression for https://github.com/erniecohen/dafny/issues/358.  Under --modular-termination,
// admitting a library checks the Z3 binary that verified it, so every command that loads libraries
// must be able to name that binary.  dafny resolve, doc, extract and format took no --solver-path, and
// so refused, on a machine with a second Z3, a library that dafny verify --solver-path admits.  The
// driver builds a recorded library with Z3 5.1.0 and loads it with resolve, audit, doc, extract,
// format, verify and translate: each admits it with --solver-path naming that Z3, and refuses it with
// --solver-path naming Z3 4.12.1.
