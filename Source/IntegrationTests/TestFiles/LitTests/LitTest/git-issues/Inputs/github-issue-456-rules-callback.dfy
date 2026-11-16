// https://github.com/erniecohen/dafny/issues/456: a least or greatest lemma cannot declare a callback bound, since its body is verified
// as its prefix lemma.
module M {
  least lemma A19(g: nat -> nat, n: nat) ensures true decreases {:global} n decreases {:callback g} n { }
  greatest lemma A20(g: nat -> nat, n: nat) ensures true decreases {:callback g} n { }
}
