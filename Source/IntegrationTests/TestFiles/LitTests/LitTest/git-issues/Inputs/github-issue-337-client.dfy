// Client of the library that github-issue-337.dfy builds.  Only Hidden fails.
module Client {
  import Lib
  lemma Revealed(k: Lib.K) ensures Lib.c == 5 && Lib.g == 6 && Lib.K.s == 7 && k.i == 8 && Lib.plain == 9 {
    reveal Lib.c, Lib.g, Lib.K.s, k.i;
  }
  lemma Hidden() ensures Lib.c == 5 {
  }
}
