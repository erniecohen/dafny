abstract module A {
  lemma P() ensures false decreases 2
  lemma R() ensures false decreases 1 { P(); }
}
