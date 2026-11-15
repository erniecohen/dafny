lemma L(i:int) requires 0 <= i < 0xD800 { assert 0 <= i < 0xD800 || 0xE000 <= i < 0x110000; var v := i as char; }
