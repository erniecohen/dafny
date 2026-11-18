// Multi-file recorded-source round trip, including two independently numbered wildcards.
// RUN: %exits-with 0 python3 %S/Inputs/github-issue-211-recorded-wildcards.py "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" %review-z3 %t.controls > %t
// RUN: %diff %s.expect %t
