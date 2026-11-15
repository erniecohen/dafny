ghost predicate P<T>(s:seq<T>) { |s|>0 } ghost function Use<T>(s:seq<T>):T requires P(s) { s[0] } lemma L<T>(s:seq<T>) requires |s|>0 { var v := Use(s); }
