# Dafny 4.11.0, review line

This fork's default branch, `review/4.11.0`, is Dafny 4.11.0 plus soundness fixes, crash fixes and opt-in improvements. Each change was reviewed and tested, and each was squash-merged as one pull request. Builds are published under [Releases](https://github.com/erniecohen/dafny/releases) as `v4.11.0+fcb2042d.review.<id>` (linux-x64 and osx-arm64, with `SHA256SUMS`). The fix-by-fix record is in [`REVIEW.md`](REVIEW.md), the plans in [`ROADMAP.md`](ROADMAP.md), and the work in [the issues](https://github.com/erniecohen/dafny/issues).

Unless marked *opt-in*, a change is on by default. Soundness fixes are on by default, and where one changes established behaviour, a hidden `--legacy-*` option restores the old behaviour.

## Headline changes since 4.11.0

**Soundness**
- Repairs of unsound axioms in the Boogie prelude and translation: single-constructor datatype equality, `Map#Items`, `Map#Glue`, box/unbox inverses, ORDINAL arithmetic, the heap-succession and allocation axioms, and the call facts of forall statements and constant fields.
- Builds against a reviewed Boogie 3.5.5 ([erniecohen/boogie](https://github.com/erniecohen/boogie)) that no longer emits unsound casts under the arguments type encoding.
- Further checks that were missing: static-call receivers inside `match`, quantifiers and lambdas; two-state recursion; ordinal-sized quantifier domains in extreme recursion; reads before `new;` in constructors; and co-recursive membership.
- Function values are known to be allocated from what they capture, not from their reads frames.
- Decreases tuples are compared component by component, using relations compatible across component types.

**Termination**
- *Opt-in* `--modular-termination`: cross-module termination checked through recorded bounds and contracts, which `.doo` libraries carry. It includes a certified standard-library core.
- *Opt-in* `--warn-unchecked-termination` reports `{:termination false}` and assumed cross-module termination.
- *Opt-in* `--decreases-orders`: `decreases e by O`, where `O` is any user-proved well-founded order (`Std.Termination.Orders.Wfo`).
- *Opt-in* `--trait-rank-decreases`: datatypes and the non-reference traits they extend are ordered by rank.

**Standard library**
- `Std` verifies with Z3 5.1.0. Each declaration's verdict is recorded and checked in CI.
- `Std.Axioms` collects the library's unproved premises in one place, including function extensionality and the completeness of `real`.
- `Std.RealCompleteness` gives least upper bounds for `iset<real>`.

**Resolver and language** (all *opt-in*)
- `--conservative-match-inference` and fixes to the refreshed resolver (`--type-system-refresh`) for crossed comparisons, match literals, subtype variance diagnostics and match pattern types.
- `--contextual-quantifier-domains`, `--contextual-lambda-domains`, `--invariant-constructor-inference`, `--synonym-refines-abstract-type`, `--ancestor-qualified-module-ids`, `--datatype-subset-conversions` and `--statement-expression-conclusions`.
- `--user-defined-operators` (milestone 1).

**Proof automation** (*opt-in*)
- `--additional-axioms`: extra facts the prelude omits, such as lambda preconditions, datatype field ranks and bounded bitvector round trips. These are gated and measured in Z3 resource units.
- `--arithmetic-solver` for program-wide Z3 arithmetic solver selection.
- `--bitvector-map-keys`.
- `--heap-free-lambda-facts`.

**Performance and tooling** (*opt-in*)
- `--verification-cache` (a persistent per-task result cache) and `--translation-cache`.
- `--verification-module-window`.
- Resolve once, verify many for multi-file requests.
- `--pipeline-profile` timing sidecars.

**Robustness**
- Dozens of crash, hang and diagnostic fixes in the resolver, cloner, refinement, translator, `.doo` serialization, and the C#, Python and JavaScript backends. Each comes with a regression test.
- `.doo` libraries now round-trip: refinement syntax, `opaque` constants and `{:auto_generated}` members survive serialization.
- Builds on .NET 10.

---

# About Dafny

[![Build and Test](https://github.com/dafny-lang/dafny/workflows/Build%20and%20Test/badge.svg)](https://github.com/dafny-lang/dafny/actions?query=workflow%3A%22Build+and+Test%22) [![Gitter](https://badges.gitter.im/dafny-lang/community.svg)](https://gitter.im/dafny-lang/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

Dafny is a **verification-ready programming language**. As you type in your program, Dafny's verifier constantly looks over your shoulder, flags any errors, shows you counterexamples, and congratulates you when your code matches your specifications. When you're done, Dafny can **compile your code to C#, Go, Python, Java, or JavaScript** (more to come!), so it can integrate with your existing workflow.

![vs-code-dafny-2 0 0-demo](https://user-images.githubusercontent.com/3601079/140799975-f3ac0925-10d9-4c14-b1a9-cd449854c6ae.gif)

Dafny will give you **assurance that your code meets the specifications you write**, while letting you write both code and specifications in the Dafny programming language itself. Since verification is an integral part of development, it will thus **reduce the risk of costly late-stage bugs** that are typically missed by testing.

Dafny has support for common programming concepts such as [classes and trait inheritance](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#sec-class-types), [inductive datatypes](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#181-inductive-datatypes) that can have methods and are suitable for pattern matching, [lazily unbounded datatypes](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#182-co-inductive-datatypes), [subset types e.g. for bounded integers](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#sec-subset-types), [lambdas](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#sec-lambda-expressions), and [immutable and mutable data structures](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#sec-collection-types).

Dafny also offers an extensive toolbox for mathematical proofs, such as [unbounded and bounded quantifiers](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#sec-forall-statement), [calculational proofs](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#1923-calc-statement), [pre- and post-conditions, termination conditions, loop invariants, and read/write specifications](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef#51-specification-clauses).

![Dafny](docs/images/dafny-logo-230.png)

This github site contains these materials:

* sources
* [binary downloads](https://github.com/dafny-lang/dafny/releases) for Windows, macOS, GNU/Linux, and FreeBSD
* the [issue tracker](https://github.com/dafny-lang/dafny/issues)
* the wiki, including [frequently asked questions](https://github.com/dafny-lang/dafny/wiki/FAQ)

Documentation about the Dafny language and tools is located
[here](https://dafny-lang.github.io/dafny).
A reference manual is available both [online](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef) and as [pdf](https://github.com/dafny-lang/dafny/blob/master/docs/DafnyRef/out/DafnyRef.pdf). (A LaTeX version can be produced if needed.)

## Community

Feel free to report issues here on GitHub or to ask for questions on our :speech_balloon: [Zulip](https://dafny.zulipchat.com/) channel. 

## Try Dafny

The easiest way to try out Dafny is to [install Dafny on your own machine in Visual Studio Code](https://github.com/dafny-lang/dafny/wiki/INSTALL#visual-studio-code)
and follow along with the [Dafny tutorial](https://dafny-lang.github.io/dafny/OnlineTutorial/guide).
You can also [download and install](https://github.com/dafny-lang/dafny/wiki/INSTALL#install-the-binaries) the Dafny CLI if you prefer to work from the command line.

## Read more

Here are some ways to get started with Dafny:

* 4-part course on the _Basics of specification and verification of code_:
  * Lecture 0: [Pre- and postconditions](https://youtu.be/oLS_y842fMc) (19:08)
  * Lecture 1: [Invariants](https://youtu.be/J0FGb6PyO_k) (20:56)
  * Lecture 2: [Binary search](https://youtu.be/-_tx3lk7yn4) (21:14)
  * Lecture 3: [Dutch National Flag algorithm](https://youtu.be/dQC5m-GZYbk) (20:33)
* New overview article: [Accessible Software Verification with Dafny](https://www.computer.org/csdl/mags/so/2017/06/mso2017060094-abs.html), IEEE Software, Nov/Dec 2017
* [Online tutorial](https://dafny-lang.github.io/dafny/OnlineTutorial/guide), focusing mostly on simple imperative programs
* [3-page tutorial notes](http://leino.science/papers/krml233.pdf) with examples (ICSE 2013)
* Dafny [Quick Reference](https://dafny-lang.github.io/dafny/QuickReference)
* Language reference for the [Dafny type system](http://leino.science/papers/krml243.html), which also describes available expressions for each type
* [Cheatsheet](https://docs.google.com/document/d/1kz5_yqzhrEyXII96eCF1YoHZhnb_6dzv-K3u79bMMis/edit?pref=2&pli=1): basic Dafny syntax on two pages
* Dafny Reference Manual [[html](https://dafny-lang.github.io/dafny/DafnyRef/DafnyRef)] [[pdf](https://github.com/dafny-lang/dafny/blob/master/docs/DafnyRef/out/DafnyRef.pdf)]
* [Dafny libraries](Source/DafnyStandardLibraries/README.md), a standard library of useful Dafny functions and lemmas
* [Dafny Power User](http://leino.science/dafny-power-user)
* Videos at [Verification Corner](https://www.youtube.com/channel/UCP2eLEql4tROYmIYm5mA27A)
* [Blog](https://dafny.org/blog)

The language itself draws pieces of influence from:

* Euclid (from the mindset of a designing a language whose programs are to be verified),
* Eiffel (like the built-in contract features),
* CLU (like its iterators, and inspiration for the out-parameter syntax),
* Java, C#, and Scala (like the classes and traits, and syntax for functions),
* ML (like the module system, and its functions and inductive datatypes), and
* Coq and VeriFast (like the ability to include co-inductive datatypes and being able to write inductive and co-inductive proofs).

## External contributions

* [Haskell-to-Dafny translator](http://www.doc.ic.ac.uk/~dcw/h2d.cgi), by Duncan White
* [Vim-loves-Dafny mode](https://github.com/mlr-msft/vim-loves-dafny) for vim, by Michael Lowell Roberts
* [Boogie-Friends Emacs mode](https://github.com/boogie-org/boogie-friends)

## Contributors

Information and instructions for potential contributors are provided [here](CONTRIBUTING.md).

## License

Dafny itself is licensed under the MIT license. (See `LICENSE.txt` in the root
directory for details.) The subdirectory `Source/Dafny/Coco` contains third
party material; see `Source/DafnyCore/Coco/LICENSE.txt` for more details.
