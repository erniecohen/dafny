// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): a library that
// declares a bodiless {:axiom} trait lemma.  Built with --inherited-trait-axioms it is a trait axiom;
// built without, it is a member that every class must implement, also for a client that uses the option.

module Library {
  trait T<X> {
    ghost function Size(x: X): nat
    lemma {:axiom} SizePositive(x: X) ensures Size(x) > 0
  }
}
