replaceable module Spec { function {:axiom} F(): int }
module Client { import Spec function G(): int { Spec.F() } method Main() { print "ok\n"; } }
module Impl replaces Spec { import Client function F(): int { Client.G() } }
