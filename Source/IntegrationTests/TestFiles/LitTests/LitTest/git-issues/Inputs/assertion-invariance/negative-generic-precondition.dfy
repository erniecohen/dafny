ghost function Use<T>(s:seq<T>):T requires |s|>0 { s[0] } lemma L() { var v := Use<int>([]); }
