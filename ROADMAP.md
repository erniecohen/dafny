# Roadmap

This fork carries Dafny 4.11.0 with fixes for its known soundness issues, on the
branch `review/4.11.0`.  [`REVIEW.md`](REVIEW.md) says what that line is and how to
check its version.  This file says where the fork is going.  The work itself is in
[the issues](https://github.com/erniecohen/dafny/issues), and
[`AGENTS.md`](AGENTS.md) says how to take an item on.

## Direction

- **Soundness fixes.**  A fix for an unsound axiom or encoding ships on
  `review/4.11.0` once it is tested, and is offered upstream as a pull request.
  Soundness fixes are the only changes that may change what Dafny does by default.
- **Bug fixes**, such as fixes for crashes in the resolver.  They ship with the
  crashing program as a test.
- **Missing axioms**, such as axioms for bitvectors.  A new axiom makes more
  things provable.  It cannot fix unsoundness, and it can introduce it.  So each
  one ships with a written soundness argument and a vacuity control, and for now
  behind one opt-in option that all added axioms share (`AGENTS.md`).
- **Improvements, opt-in only.**  Anything else is a new option, with the defaults
  unchanged.  Under default options, a program gets the verdicts and resource
  counts that it gets from 4.11.0 with the soundness fixes.  For example, a
  `dafny verify` that uses less memory would be a new option, not a change to the
  command.
- **Upstream is where fixes belong.**  Bug fixes, soundness fixes among them, are
  offered to dafny-lang/dafny and boogie-org/boogie as pull requests, one per fix.
  Anything else goes upstream only with the owner's approval.  This line carries
  each fix until upstream has it.
- **The base stays Dafny 4.11.0 for now**
  ([issue 24](https://github.com/erniecohen/dafny/issues/24)).

## Where the work is

[The open issues](https://github.com/erniecohen/dafny/issues), by label:

| label | what it marks |
|---|---|
| [`soundness`](https://github.com/erniecohen/dafny/labels/soundness) | an unsound axiom or encoding: something false can be proved |
| [`crash`](https://github.com/erniecohen/dafny/labels/crash) | Dafny or Boogie crashes, for example in the resolver |
| [`completeness`](https://github.com/erniecohen/dafny/labels/completeness) | a missing axiom: something true cannot be proved |
| [`enhancement`](https://github.com/erniecohen/dafny/labels/enhancement) | an improvement, opt-in only |
| [`upstream`](https://github.com/erniecohen/dafny/labels/upstream) | has a dafny-lang or boogie-org issue or pull request |
| [`needs-owner-approval`](https://github.com/erniecohen/dafny/labels/needs-owner-approval) | waits for the owner's approval, before the work or before an upstream pull request |
| [`in-progress`](https://github.com/erniecohen/dafny/labels/in-progress) | someone holds it: see the assignee and the comments |

The pull requests upstream that await review are listed in
[issue 23](https://github.com/erniecohen/dafny/issues/23).
