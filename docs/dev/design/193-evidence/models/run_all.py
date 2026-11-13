#!/usr/bin/env python3
"""Run every #193 mathematical-model check; no external dependencies.

This is finite model testing, not a proof of universal validity or of the
implementation's emitted Boogie/Z3 context. Assertion failures exit nonzero.
"""
import argparse
import hashlib
import json
import random
from pathlib import Path

import model as m

IDS = ("A", "C", "+", "-", "*", "/", "%", "L", "R", "<=")
SEED = 193_2026_11_11


class Checks:
    def __init__(self, n, mode):
        self.n, self.mode = n, mode
        self.schema = {x: [0, 0] for x in IDS}
        self.composed = {x: [0, 0] for x in IDS}
        self.extra = 0
        self.pairs = self.words = self.counts = 0

    def eq(self, label, got, want):
        if got != want:
            raise AssertionError((self.n, self.mode, label, got, want))
        self.extra += 1

    def fact(self, name, active, got=None, want=None, composed=False):
        target = self.composed if composed else self.schema
        target[name][0 if active else 1] += 1
        if active and got != want:
            raise AssertionError((self.n, self.mode, name, composed, got, want))

    def report(self):
        return {"width": self.n, "mode": self.mode, "operand_pairs": self.pairs,
                "unary_inputs": self.words, "word_count_inputs": self.counts,
                "schema_active_inactive": self.schema,
                "composed_active_inactive": self.composed,
                "additional_equalities": self.extra}


def binary(c, x, y):
    n, mod = c.n, m.p(c.n)
    s, t = m.bits(n, x), m.bits(n, y)
    a, b = m.value(s), m.value(t)
    c.eq("unsigned inputs", (a, b), (x, y))
    c.pairs += 1
    refs = {"A": m.and_nat(n, a, b), "+": (a+b) % mod,
            "-": (a-b) % mod, "*": (a*b) % mod, "<=": a <= b}
    if b:
        refs.update({"/": a // b, "%": a % b})
    for op in ("A", "+", "-", "*", "/", "%", "<="):
        active = op not in ("/", "%") or b != 0
        got = m.native(n, op, x, y)
        c.fact(op, active, got, refs.get(op))
        # These are the composed forms at all range-checked source operands.
        c.fact(op, active, m.native(n, op, m.checked_cast(n, a),
                                  m.checked_cast(n, b)), refs.get(op), True)
    # Independent sequence algorithms validate the native side itself.
    c.eq("A digit interpretation", m.native(n, "A", x, y),
         m.value(m.bool_op("A", s, t)))
    c.eq("addition ripple carry", m.native(n, "+", x, y), m.value(m.add_bits(s, t)))
    c.eq("subtraction ripple carry", m.native(n, "-", x, y), m.value(m.sub_bits(s, t)))
    c.eq("multiplication digit fold", m.native(n, "*", x, y), m.mul_digits(s, t))
    c.eq("division digit algorithm", (m.native(n, "/", x, y), m.native(n, "%", x, y)),
         m.div_digits(s, t))
    c.eq("unsigned lexicographic order", m.native(n, "<=", x, y), m.le_bits(s, t))
    for op, ref in (("OR", m.or_nat), ("XOR", m.xor_nat), ("ANDNOT", m.and_not_nat)):
        got = m.native(n, op, x, y)
        c.eq(op + " reference", got, ref(n, a, b))
        c.eq(op + " digit interpretation", got, m.value(m.bool_op(op, s, t)))
    c.eq("injectivity", (a == b), (s == t))


def unary(c, x):
    n, mod = c.n, m.p(c.n)
    s, a = m.bits(n, x), x
    c.words += 1
    c.eq("bit length", len(s), n)
    c.eq("value(bits(x))", m.value(s), x)
    c.eq("bits(value(s))", m.bits(n, m.value(s)), s)
    got = m.native(n, "C", x)
    c.fact("C", True, got, mod-1-a)
    c.fact("C", True, m.native(n, "C", m.checked_cast(n, a)), mod-1-a, True)
    c.eq("C reference", got, m.not_nat(n, a))
    c.eq("C digit interpretation", got, m.value(tuple(not b for b in s)))
    c.eq("population count", m.pop_nat(n, a), sum(s))
    for i, bit in enumerate(s):
        c.eq("single bit", bool(m.native(n, "R", x, k=i) & 1),
             (a // m.p(i)) % 2 == 1)
        c.eq("indexed bit", (a // m.p(i)) % 2 == 1, bit)


def counts(c, x, k):
    n, mod = c.n, m.p(c.n)
    s, a = m.bits(n, x), x
    c.counts += 1
    active = 0 <= k <= n
    for op, ref, left in (("L", m.shl_nat, True), ("R", m.shr_nat, False)):
        got = m.native(n, op, x, k=k)
        want = ((a*m.p(k)) % mod if left else a // m.p(k)) if active else None
        c.fact(op, active, got, want)
        c.fact(op, active, m.native(n, op, m.checked_cast(n, a), k=k), want, True)
        # Source count N+1 is illegal. Test its total reference separately;
        # do not assert a missing guard's conclusion or assume no count wrapping.
        c.eq(op + " total reference/sequence", ref(n, a, k),
             m.value(m.shift_bits(s, k, left)))
        if active:
            c.eq(op + " native/sequence", got, m.value(m.shift_bits(s, k, left)))
    for op, ref, left in (("ROL", m.rol_nat, True), ("ROR", m.ror_nat, False)):
        c.eq(op + " reference/sequence", ref(n, a, k),
             m.value(m.rotate_bits(s, k, left)))
        if active:
            c.eq(op + " native/sequence", m.native(n, op, x, k=k),
                 m.value(m.rotate_bits(s, k, left)))


def out_of_range(c, a, b, k):
    """Evaluate guarded composed facts, including false guards, explicitly."""
    n, mod = c.n, m.p(c.n)
    fit_a, fit_b = 0 <= a < mod, 0 <= b < mod
    u, v = a % mod, b % mod
    refs = {"A": m.and_nat(n, a, b), "+": (a+b) % mod,
            "-": (a-b) % mod, "*": (a*b) % mod, "<=": a <= b}
    for op in ("A", "+", "-", "*", "<="):
        c.fact(op, fit_a and fit_b, m.native(n, op, a, b), refs[op], True)
    c.fact("C", fit_a, m.native(n, "C", a), mod-1-a, True)
    for op in ("/", "%"):
        active = fit_a and fit_b and b != 0
        want = (a // b if op == "/" else a % b) if active else None
        c.fact(op, active, m.native(n, op, a, b), want, True)
    for op in ("L", "R"):
        active = fit_a and 0 <= k <= n
        want = (((a*m.p(k)) % mod) if op == "L" else a // m.p(k)) if active else None
        c.fact(op, active, m.native(n, op, a, k=k), want, True)
    c.eq("total bits normalize signed a", m.value(m.bits(n, a)), u)
    c.eq("total bits normalize signed b", m.value(m.bits(n, b)), v)
    # This periodic extension is true even though such source casts are illegal.
    c.eq("A periodic extension", m.native(n, "A", a, b), m.and_nat(n, a, b))


def exhaustive(n):
    c, mod = Checks(n, "exhaustive"), m.p(n)
    for x in range(mod):
        unary(c, x)
        for y in range(mod):
            binary(c, x, y)
        for k in range(n+2):
            counts(c, x, k)
    boundary = (-mod-1, -1, 0, 1, mod-1, mod, mod+1, 2*mod)
    for a in boundary:
        for b in boundary:
            for k in range(-1, n+2):
                out_of_range(c, a, b, k)
    return c.report()


def random_width(n, sample_count):
    c, mod = Checks(n, "random"), m.p(n)
    rng = random.Random(SEED+n)
    for j in range(sample_count):
        x, y, k = rng.randrange(mod), rng.randrange(mod), rng.randrange(n+1)
        binary(c, x, y)
        unary(c, x)
        counts(c, x, k)
        # All random words additionally exercise both endpoint counts and N+1.
        for endpoint in (0, n, n+1):
            if endpoint != k:
                counts(c, x, endpoint)
        if j % 128 == 0:
            out_of_range(c, x-mod, y+mod, n+1)
            out_of_range(c, x+mod, y-mod, -1)
    # Deterministic edge cases supplement, rather than replace, the random pairs.
    for x in (0, 1, mod-1, mod//2, mod//2-1):
        for y in (0, 1, mod-1, mod//2, mod//2-1):
            binary(c, x, y)
        for k in range(n+2):
            counts(c, x, k)
    r = c.report()
    r.update({"seed": SEED+n, "random_operand_pairs": sample_count})
    return r


def expect_exception(kind, f):
    try:
        f()
    except kind:
        return kind.__name__
    raise AssertionError("expected " + kind.__name__)


def guards():
    out = []
    # Dividing by zero is undefined in the source arithmetic. SMT BV division
    # is total. Giving mathematical div(0,0) a common width-independent value
    # cannot satisfy unguarded quotient bridges at widths 1 and 2 simultaneously.
    ex = expect_exception(ZeroDivisionError, lambda: 0 // 0)
    q1, q2 = m.native(1, "/", 0, 0), m.native(2, "/", 0, 0)
    assert q1 != q2
    out.append({"guard": "nonzero divisor", "kind": "definedness and quotient inconsistency",
                "integer_division": ex, "bv1_0_div_0": q1, "bv2_0_div_0": q2,
                "explanation": "An unguarded bridge would identify both with the same int div(0,0)."})
    out.append({"guard": "nonzero remainder divisor", "kind": "definedness",
                "integer_remainder": expect_exception(ZeroDivisionError, lambda: 1 % 0),
                "bv3_1_rem_0": m.native(3, "%", 1, 0)})
    left, right = m.native(3, "L", 1, k=8), (1*m.p(8)) % 8
    assert left != right
    out.append({"guard": "count upper bound", "N": 3, "x": 1, "k": 8,
                "I_N(k)": 0, "native_left": left, "integer_right": right,
                "explanation": "k<=N prevents count wrapping; it is sufficient, not minimal."})
    left, right = m.native(3, "R", 1, k=8), 1 // m.p(8)
    assert left != right
    out.append({"guard": "count upper bound (R)", "N": 3, "x": 1, "k": 8,
                "native_left": left, "integer_right": right})
    out.append({"guard": "count lower bound", "N": 3, "k": -1,
                "P(k)": expect_exception(ValueError, lambda: m.p(-1)),
                "explanation": "The power argument must be natural; total references branch before P(k)."})
    for a in (-1, 8):
        left, right = m.native(3, "C", a), 7-a
        assert left != right
        out.append({"guard": "cast range", "N": 3, "a": a,
                    "native_complement": left, "unguarded_C_rhs": right,
                    "round_trip": a % 8,
                    "source_cast": expect_exception(ValueError, lambda: m.checked_cast(3, a))})
    assert m.native(3, "<=", 8, 1) != (8 <= 1)
    out.append({"guard": "cast range (order)", "N": 3, "a": 8, "b": 1,
                "native_order": True, "integer_order": False})
    out.append({"guard": "width zero", "N": 0,
                "unguarded_rotation_k_mod_N": expect_exception(ZeroDivisionError, lambda: 0 % 0),
                "native_sort": expect_exception(ValueError, lambda: m.native(0, "A", 0, 0)),
                "explanation": "Use the separate singleton semantics; do not create SMT BitVec 0."})
    # Prevent overstating the examples: show valid equations outside two guards.
    assert m.native(3, "L", 1, k=4) == (1*m.p(4)) % 8
    assert m.native(3, "A", -1, 8) == m.and_nat(3, -1, 8)
    out.append({"qualification": "Not every guard is necessary for every schema",
                "N3_k4_L_still_true": True, "out_of_range_A_periodicity_still_true": True})
    return out


def zero_width():
    checked = 0
    for a in (-9, -1, 0, 1, 9):
        assert m.bits(0, a) == () and m.value(()) == 0
        assert m.and_nat(0, a, -1) == m.not_nat(0, a) == 0
        assert m.or_nat(0, a, 9) == m.xor_nat(0, a, 9) == m.and_not_nat(0, a, 9) == 0
        assert m.pop_nat(0, a) == 0
        for k in (-1, 0, 1, 17):
            assert m.shl_nat(0, a, k) == m.shr_nat(0, a, k) == 0
            assert m.rol_nat(0, a, k) == m.ror_nat(0, a, k) == 0
            checked += 1
    return {"width": 0, "singleton_word_count_cases": checked,
            "positive_width_schemata_emitted": 0}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--random-samples", type=int, default=100000)
    ap.add_argument("--output", type=Path, default=Path(__file__).with_name("results.json"))
    args = ap.parse_args()
    if args.random_samples < 100000:
        ap.error("acceptance requires at least 100000 random pairs at EACH large width")
    results = []
    for n in range(1, 9):
        results.append(exhaustive(n))
        print("PASS exhaustive width", n, flush=True)
    for n in (16, 32, 64):
        results.append(random_width(n, args.random_samples))
        print("PASS random width", n, "pairs", args.random_samples, flush=True)
    base = Path(__file__).parent
    report = {"status": "PASS", "scope": "Finite executable model checks, not a proof or a solver run",
              "schema_count_order": ["guard true (equation checked)", "guard false (implication checked)"],
              "exhaustive_counts": "Each word and pair in [0,2^N); each word/count in 0..N+1 for N=1..8",
              "random_counts": "100000 or more independent uniform word pairs per width; one uniform valid count and 0,N,N+1 per random word",
              "source_sha256": {name: hashlib.sha256((base/name).read_bytes()).hexdigest()
                                for name in ("model.py", "run_all.py")},
              "widths": results, "zero_width": zero_width(), "guard_counterexamples": guards()}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, separators=(",", ":"), sort_keys=True)+"\n")
    print("PASS all ten schemata, composed casts, derived laws, zero-width semantics and guard counterexamples")


if __name__ == "__main__":
    main()
