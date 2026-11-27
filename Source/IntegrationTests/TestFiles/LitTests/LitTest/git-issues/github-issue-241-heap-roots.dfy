// Offline graph controls; this test does not invoke the verifier or add obligations.
// RUN: python3 "%repositoryRoot/.github/review/heap-retention.py" -- --self-test > "%t"
// RUN: %diff "%s.expect" "%t"
