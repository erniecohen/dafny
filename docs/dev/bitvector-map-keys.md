# Bitvector map lookup keys

The default-off option `--bitvector-map-keys` corrects the verifier translation
of map and imap lookups with bitvector keys. It emits no new axioms.

## Cause and correction

An update of `map<bv64,bool>` at a key `k` uses `$Box(k)`, with native
bitvector sort. The old lookup translation converts every bitvector index to
an integer before considering the indexed collection. Thus lookup uses
`$Box(nat_from_bv64(k))`, with native integer sort, while its domain check uses
`$Box(k)`. The existing map update axiom relates an update and a lookup only
when their boxed keys are equal. The two differently typed values are not the
same key, even when their unsigned numeric values agree.

With the option enabled, lookup of map and imap values keeps the translated key
and boxes it at its declared domain type, just as update, display and domain
membership do. Sequence and array positions still use integer conversion.
The existing well-formedness checks, map update/select formulas, type checks and
boxing axioms remain unchanged. Multiset lookup is outside this change.

## Semantic argument and compatibility

A map with domain type T is a partial function on T. For a bitvector domain its
keys are bitvectors, so lookup must receive the same bitvector value as update.
Preserving the native key is a direct translation of that rule and requires no
numeric round-trip assumption, identification of boxed integers with boxed
bitvectors, or additional fact. It is independent of every family enabled by
`--additional-axioms`, and the regression checks both settings.

With the option disabled, the conversion branch, translated expressions and
emitted formulas are unchanged. The option is checked before changing the
expression; default verification costs must retain the existing encoding.
Regression controls cover both resolver modes, literal and variable keys,
several widths including bv0, finite and infinite maps, unequal key preservation,
domain membership and false assertions after a successful update/read.
