// A client of github-issue-461-lib.dfy, verified against the library built under --modular-termination.
module C { import M
  lemma Use() ensures M.Even(4) && M.Always(3) { M.EvenPlusTwo(2); M.AlwaysHolds(3); }
}
