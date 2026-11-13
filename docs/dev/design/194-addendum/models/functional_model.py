#!/usr/bin/env python3
"""Q2 executable model: surface evaluation versus explicit named-call lowering.

Python 3 standard library only. Run: python3 functional_model.py
Binding/type/contract checks are assumed complete. Logged probes observe order
and duplication in this model; they do not make Dafny functions effectful.
The model proves neither resolver correctness nor Dafny verifier soundness.
"""
from dataclasses import dataclass
from itertools import product
import unittest


@dataclass(frozen=True)
class V:
    xs: tuple

    def put(self, i, value):
        if not 0 <= i < len(self.xs):
            raise ValueError("put precondition")
        return V(self.xs[:i] + (value,) + self.xs[i + 1:])

    def between(self, lo, hi):
        if not 0 <= lo <= hi <= len(self.xs):
            raise ValueError("between precondition")
        return V(self.xs[lo:hi])

    def from_(self, lo):
        if not 0 <= lo <= len(self.xs):
            raise ValueError("from precondition")
        return V(self.xs[lo:])

    def until(self, hi):
        if not 0 <= hi <= len(self.xs):
            raise ValueError("until precondition")
        return V(self.xs[:hi])

    def all_(self):
        return self


@dataclass(frozen=True)
class Grid:
    # Immutable sorted association list, so equality observes persistent state.
    cells: tuple = ()

    def put(self, key, value):
        cells = dict(self.cells)
        cells[key] = value
        return Grid(tuple(sorted(cells.items())))


class NoLength:
    """A library may give slices meanings unrelated to sequence length."""
    def between(self, lo, hi):
        return ("both", lo, hi)

    def from_(self, lo):
        return ("lower", lo)

    def until(self, hi):
        return ("upper", hi)

    def all_(self):
        return ("neither",)


class DifferentResult:
    def put(self, key, value):
        return V((key, value))


@dataclass(frozen=True)
class Atom:
    label: str
    value: object


@dataclass(frozen=True)
class Update:
    receiver: object
    index: object
    value: object


@dataclass(frozen=True)
class Slice:
    receiver: object
    lo: object = None  # None is AST absence, never an argument sent to the slot.
    hi: object = None


@dataclass(frozen=True)
class Call:
    receiver: object
    name: str
    args: tuple


def invoke(receiver, name, args, trace):
    trace.append("call " + name)
    return getattr(receiver, name)(*args)


def surface(expr, trace):
    if isinstance(expr, Atom):
        trace.append(expr.label)
        return expr.value
    if isinstance(expr, Update):
        receiver = surface(expr.receiver, trace)
        index = surface(expr.index, trace)
        value = surface(expr.value, trace)
        return invoke(receiver, "put", (index, value), trace)
    if isinstance(expr, Slice):
        receiver = surface(expr.receiver, trace)
        if expr.lo is not None and expr.hi is not None:
            lo = surface(expr.lo, trace)
            hi = surface(expr.hi, trace)
            return invoke(receiver, "between", (lo, hi), trace)
        if expr.lo is not None:
            return invoke(receiver, "from_", (surface(expr.lo, trace),), trace)
        if expr.hi is not None:
            return invoke(receiver, "until", (surface(expr.hi, trace),), trace)
        return invoke(receiver, "all_", (), trace)
    raise TypeError(type(expr))


def lower(expr):
    if isinstance(expr, Atom):
        return expr
    if isinstance(expr, Update):
        return Call(lower(expr.receiver), "put", (lower(expr.index), lower(expr.value)))
    if isinstance(expr, Slice):
        # Fixed shape dispatch: no optional value, length query or dummy bound.
        if expr.lo is None:
            name = "all_" if expr.hi is None else "until"
            args = () if expr.hi is None else (lower(expr.hi),)
        else:
            name = "from_" if expr.hi is None else "between"
            args = (lower(expr.lo),) if expr.hi is None else (lower(expr.lo), lower(expr.hi))
        return Call(lower(expr.receiver), name, args)
    raise TypeError(type(expr))


def named(expr, trace):
    if isinstance(expr, Atom):
        trace.append(expr.label)
        return expr.value
    receiver = named(expr.receiver, trace)
    args = tuple(named(arg, trace) for arg in expr.args)
    return invoke(receiver, expr.name, args, trace)


class DesugaringTests(unittest.TestCase):
    def check(self, expr, result, events=None):
        surface_trace, named_trace = [], []
        self.assertEqual(surface(expr, surface_trace), result)
        self.assertEqual(named(lower(expr), named_trace), result)
        self.assertEqual(surface_trace, named_trace)
        if events is not None:
            self.assertEqual(surface_trace, events)

    def test_update_evaluates_once_in_order(self):
        self.check(Update(Atom("x", V((1, 2))), Atom("i", 0), Atom("v", 7)),
                   V((7, 2)), ["x", "i", "v", "call put"])

    def test_nested_updates_use_previous_result(self):
        expr = Update(Update(Atom("x", V((1, 2))), Atom("i", 0), Atom("v", 7)),
                      Atom("j", 1), Atom("w", 8))
        self.check(expr, V((7, 8)), ["x", "i", "v", "call put", "j", "w", "call put"])

    def test_duplicate_key_is_two_ordered_calls(self):
        expr = Update(Update(Atom("x", V((1, 2))), Atom("i", 0), Atom("v", 7)),
                      Atom("j", 0), Atom("w", 8))
        self.check(expr, V((8, 2)))

    def test_alias_remains_original(self):
        x = V((1, 2))
        alias = x
        self.check(Update(Atom("x", x), Atom("i", 0), Atom("v", 7)), V((7, 2)))
        self.assertIs(alias, x)
        self.assertEqual(alias.xs, (1, 2))

    def test_later_operand_can_read_original_alias(self):
        x = V((1, 2))
        expr = Update(Update(Atom("x", x), Atom("i", 0), Atom("v", 7)),
                      Atom("j", 1), Atom("old x[0]", x.xs[0]))
        self.check(expr, V((7, 1)))

    def test_both_bounds(self):
        self.check(Slice(Atom("x", V((1, 2, 3))), Atom("lo", 1), Atom("hi", 3)),
                   V((2, 3)), ["x", "lo", "hi", "call between"])

    def test_lower_bound(self):
        self.check(Slice(Atom("x", V((1, 2, 3))), Atom("lo", 1)),
                   V((2, 3)), ["x", "lo", "call from_"])

    def test_upper_bound(self):
        self.check(Slice(Atom("x", V((1, 2, 3))), hi=Atom("hi", 2)),
                   V((1, 2)), ["x", "hi", "call until"])

    def test_no_bounds(self):
        self.check(Slice(Atom("x", V((1, 2, 3)))), V((1, 2, 3)), ["x", "call all_"])

    def test_empty_slice(self):
        self.check(Slice(Atom("x", V((1, 2, 3))), Atom("lo", 2), Atom("hi", 2)), V(()))

    def test_slice_then_update(self):
        expr = Update(Slice(Atom("x", V((1, 2, 3))), Atom("lo", 1)),
                      Atom("i", 0), Atom("v", 9))
        self.check(expr, V((9, 3)), ["x", "lo", "call from_", "i", "v", "call put"])

    def test_update_then_slice(self):
        expr = Slice(Update(Atom("x", V((1, 2, 3))), Atom("i", 0), Atom("v", 9)),
                     hi=Atom("hi", 2))
        self.check(expr, V((9, 2)), ["x", "i", "v", "call put", "hi", "call until"])

    def test_tuple_key_is_one_argument(self):
        self.check(Update(Atom("x", Grid()), Atom("key", (1, 2)), Atom("v", 9)),
                   Grid((((1, 2), 9),)), ["x", "key", "v", "call put"])

    def test_result_type_need_not_equal_owner(self):
        expr = Update(Update(Atom("x", DifferentResult()), Atom("key", 4), Atom("v", 9)),
                      Atom("i", 1), Atom("w", 3))
        self.check(expr, V((4, 3)))

    def test_omission_does_not_invent_zero_or_length(self):
        x = Atom("x", NoLength())
        self.check(Slice(x), ("neither",), ["x", "call all_"])
        self.check(Slice(x, Atom("lo", "start")), ("lower", "start"))
        self.check(Slice(x, hi=Atom("hi", "end")), ("upper", "end"))
        self.check(Slice(x, Atom("lo", "a"), Atom("hi", "z")), ("both", "a", "z"))

    def test_precondition_failure_does_not_fallback(self):
        expr = Update(Atom("x", V((1,))), Atom("i", 1), Atom("v", 9))
        for evaluator, tree in ((surface, expr), (named, lower(expr))):
            events = []
            with self.assertRaisesRegex(ValueError, "put precondition"):
                evaluator(tree, events)
            self.assertEqual(events, ["x", "i", "v", "call put"])

    def test_inner_failure_prevents_outer_evaluation(self):
        expr = Update(Update(Atom("x", V((1,))), Atom("i", 1), Atom("v", 9)),
                      Atom("j", 0), Atom("w", 8))
        for evaluator, tree in ((surface, expr), (named, lower(expr))):
            events = []
            with self.assertRaises(ValueError):
                evaluator(tree, events)
            self.assertEqual(events, ["x", "i", "v", "call put"])

    def test_exhaustive_small_chains(self):
        # 2^3 starting vectors x 3^2 keys x 2^2 replacement values = 288 cases.
        checked = 0
        for values in product(range(2), repeat=3):
            for i, j, v, w in product(range(3), range(3), range(2), range(2)):
                expected = list(values)
                expected[i] = v
                expected[j] = w
                expr = Update(Update(Atom("x", V(values)), Atom("i", i), Atom("v", v)),
                              Atom("j", j), Atom("w", w))
                self.check(expr, V(tuple(expected)))
                checked += 1
        self.assertEqual(checked, 288)


if __name__ == "__main__":
    unittest.main(verbosity=2)
