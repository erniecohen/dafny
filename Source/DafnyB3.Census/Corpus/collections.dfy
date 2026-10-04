lemma Collections(s: set<int>, q: seq<int>, b: multiset<int>, m: map<int, int>, im: imap<int,int>, infiniteSet: iset<int>) {
 assert s + s == s; assert q + [] == q; assert b + multiset{} == b;
 if 0 in m { assert m[0] == m[0]; }
 if 0 in im { assert im[0] == im[0]; }
 assert infiniteSet + infiniteSet == infiniteSet;
}
