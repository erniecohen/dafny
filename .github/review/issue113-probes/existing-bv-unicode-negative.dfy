lemma NarrowSurrogate() { var c := (0xD800 as bv16) as char; }
lemma WideSurrogate() { var c := (0xD800 as bv22) as char; }
lemma AboveUnicode() { var c := (0x110000 as bv22) as char; }
