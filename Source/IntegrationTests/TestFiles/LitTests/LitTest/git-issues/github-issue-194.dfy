// RUN: python3 "%S/Inputs/github-issue-194.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"

// Source-only fallback function operators: https://github.com/erniecohen/dafny/issues/194.
// The driver checks successes, resolution rejections, and genuine verification failures
// separately, retaining every command, output and exit status in its output directory.
