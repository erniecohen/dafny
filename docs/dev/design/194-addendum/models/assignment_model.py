"""Executable model of Q4's proposed lowering; standard-library Python only.

The trace is instrumentation, not permission for side effects in Dafny functions.
Expressions/functional updaters otherwise have no effects. Locations use object
identity; value results use structural equality, matching the modeled value type.
This checks the specified expansion, not a Dafny implementation or SMT encoding.
Run: python3 assignment_model.py
"""

from dataclasses import dataclass
import unittest


class ResolutionError(Exception):
    pass


class AliasConflict(Exception):
    pass


class FrameError(Exception):
    pass


@dataclass(frozen=True)
class Loc:
    storage: dict
    name: object

    def key(self):
        return id(self.storage), self.name

    def read(self):
        return self.storage[self.name]

    def write(self, value):
        self.storage[self.name] = value


@dataclass
class Target:
    root: object
    indices: tuple = ()
    updater: object = None
    writable: bool = True


def pure_assignment(targets, rhss, trace, allowed=None):
    """Capture roots/old values/indices, RHSs, replacements, then parallel store.

    A target with updater=None is an ordinary assignment target. Slot discovery
    and writable-root checking precede executable evaluation. all user indexed
    targets already have their frozen updater here. No method setters enter.
    """
    if len(targets) != len(rhss):
        raise ResolutionError("assignment arities differ")
    if any(not t.writable or t.root is None for t in targets):
        raise ResolutionError("pure update needs an ordinary writable root")
    if any(t.indices and t.updater is None for t in targets):
        raise ResolutionError("no updater slot at this index arity")
    captured = []
    for pos, target in enumerate(targets):
        loc = target.root()
        trace.append(("root", pos))
        old = loc.read() if target.updater else None
        if target.updater:
            trace.append(("old", pos))
        indices = []
        for k, expr in enumerate(target.indices):
            indices.append(expr())
            trace.append(("index", pos, k))
        captured.append((loc, old, tuple(indices)))
    values = []
    for pos, expr in enumerate(rhss):
        values.append(expr())
        trace.append(("rhs", pos))
    replacements = []
    for pos, (target, (_, old, indices), value) in enumerate(
            zip(targets, captured, values)):
        if target.updater:
            value = target.updater(old, *indices, value)
            trace.append(("call", pos))
        replacements.append(value)
    for pos, (loc, _, _) in enumerate(captured):
        if allowed is not None and loc.key() not in allowed:
            raise FrameError("root write exceeds enclosing modifies")
        for previous in range(pos):
            if (loc.key() == captured[previous][0].key()
                    and replacements[pos] != replacements[previous]):
                raise AliasConflict("same root, unequal complete replacements")
    for pos, ((loc, _, _), value) in enumerate(zip(captured, replacements)):
        loc.write(value)
        trace.append(("store", pos))


def setter_assignment(targets, rhs, trace, invoke, permitted=True):
    """Singleton receiver + indices + pure RHS, then one ordinary method call."""
    if len(targets) != 1:
        raise ResolutionError("method setter in multi-assignment")
    receiver, indices = targets[0]
    owner = receiver()
    trace.append("receiver")
    arguments = []
    for k, expr in enumerate(indices):
        arguments.append(expr())
        trace.append(("index", k))
    value = rhs()
    trace.append("rhs")
    if not permitted:
        raise FrameError("callee modifies exceeds caller modifies")
    invoke(owner, *arguments, value)
    trace.append("setter")


def replace(old, index, value):
    if not 0 <= index < len(old):
        raise ValueError("updater precondition")
    return old[:index] + (value,) + old[index + 1:]


def indexed(storage, name, *indices, updater=replace):
    return Target(lambda: Loc(storage, name), tuple(lambda i=i: i for i in indices), updater)


class Cases(unittest.TestCase):
    def test_single_local_order(self):
        state, trace = {"x": (1, 2)}, []
        pure_assignment([indexed(state, "x", 1)], [lambda: 9], trace)
        self.assertEqual(state["x"], (1, 9))
        self.assertEqual(trace, [("root", 0), ("old", 0), ("index", 0, 0),
                                 ("rhs", 0), ("call", 0), ("store", 0)])

    def test_field_root_captured_once(self):
        obj, calls, trace = {"value": (3, 4)}, [], []
        def receiver():
            calls.append("receiver")
            return Loc(obj, "value")
        pure_assignment([Target(receiver, (lambda: 0,), replace)], [lambda: 8], trace)
        self.assertEqual(calls, ["receiver"])
        self.assertEqual(obj["value"], (8, 4))

    def test_intrinsic_array_root_captured_once(self):
        array, calls, trace = {0: (1, 2), 1: (3, 4)}, [], []
        def root():
            calls.extend(["array", "outer-index"])
            return Loc(array, 1)
        pure_assignment([Target(root, (lambda: 0,), replace)], [lambda: 7], trace)
        self.assertEqual(calls, ["array", "outer-index"])
        self.assertEqual(array, {0: (1, 2), 1: (7, 4)})

    def test_two_root_swap_uses_old_values(self):
        state, trace = {"x": (1, 2), "y": (8, 9)}, []
        pure_assignment([indexed(state, "x", 0), indexed(state, "y", 1)],
                        [lambda: state["y"][1], lambda: state["x"][0]], trace)
        self.assertEqual(state, {"x": (9, 2), "y": (8, 1)})
        self.assertLess(trace.index(("call", 1)), trace.index(("store", 0)))

    def test_same_root_different_indices_do_not_merge(self):
        state, trace = {"x": (1, 2)}, []
        with self.assertRaises(AliasConflict):
            pure_assignment([indexed(state, "x", 0), indexed(state, "x", 1)],
                            [lambda: 7, lambda: 8], trace)
        self.assertEqual(state["x"], (1, 2))
        self.assertFalse(any(event[0] == "store" for event in trace))

    def test_same_root_equal_replacements_are_legal(self):
        state, trace = {"x": (1, 2)}, []
        pure_assignment([indexed(state, "x", 0), indexed(state, "x", 0)],
                        [lambda: 7, lambda: 7], trace)
        self.assertEqual(state["x"], (7, 2))

    def test_aliases_are_object_identity_not_source_names(self):
        p = q = {"value": (1, 2)}
        with self.assertRaises(AliasConflict):
            pure_assignment([indexed(p, "value", 0), indexed(q, "value", 1)],
                            [lambda: 7, lambda: 8], [])
        self.assertEqual(p["value"], (1, 2))

    def test_distinct_but_equal_objects_are_distinct_roots(self):
        p, q = {"value": (1, 2)}, {"value": (1, 2)}
        pure_assignment([indexed(p, "value", 0), indexed(q, "value", 1)],
                        [lambda: 7, lambda: 8], [])
        self.assertEqual((p["value"], q["value"]), ((7, 2), (1, 8)))

    def test_mixed_direct_target_shares_root_alias_check(self):
        state = {"x": (1, 2)}
        ordinary = Target(lambda: Loc(state, "x"))
        with self.assertRaises(AliasConflict):
            pure_assignment([ordinary, indexed(state, "x", 0)],
                            [lambda: (4, 5), lambda: 9], [])
        self.assertEqual(state["x"], (1, 2))

    def test_index_variable_uses_pre_statement_value(self):
        state = {"i": 0, "x": (1, 2)}
        targets = [Target(lambda: Loc(state, "i")),
                   Target(lambda: Loc(state, "x"), (lambda: state["i"],), replace)]
        pure_assignment(targets, [lambda: 1, lambda: 7], [])
        self.assertEqual(state, {"i": 1, "x": (7, 2)})

    def test_multi_index_value_updater(self):
        state = {"x": ((1, 2), (3, 4))}
        def put2(old, row, col, value):
            return replace(old, row, replace(old[row], col, value))
        pure_assignment([indexed(state, "x", 1, 0, updater=put2)], [lambda: 9], [])
        self.assertEqual(state["x"], ((1, 2), (9, 4)))

    def test_updater_may_replace_unrelated_elements(self):
        state = {"x": (1, 2)}
        def overwrite_all(old, ignored_index, value):
            return (value,) * len(old)
        pure_assignment([indexed(state, "x", 0, updater=overwrite_all)], [lambda: 9], [])
        self.assertEqual(state["x"], (9, 9))  # No implicit lens law.

    def test_nonwritable_root_rejected_before_evaluation(self):
        trace = []
        with self.assertRaises(ResolutionError):
            pure_assignment([Target(None, (lambda: 0,), replace, False)],
                            [lambda: trace.append("evaluated")], trace)
        self.assertEqual(trace, [])

    def test_missing_updater_rejected_before_evaluation(self):
        trace, state = [], {"x": (1, 2)}
        with self.assertRaises(ResolutionError):
            pure_assignment([indexed(state, "x", 0, updater=None)], [lambda: 9], trace)
        self.assertEqual(trace, [])

    def test_root_frame_required_before_any_store(self):
        state = {"x": (1, 2), "y": (3, 4)}
        with self.assertRaises(FrameError):
            pure_assignment([indexed(state, "x", 0), indexed(state, "y", 0)],
                            [lambda: 9, lambda: 8], [], {Loc(state, "x").key()})
        self.assertEqual(state, {"x": (1, 2), "y": (3, 4)})

    def test_updater_precondition_still_checked(self):
        state = {"x": (1, 2)}
        with self.assertRaises(ValueError):
            pure_assignment([indexed(state, "x", 2)], [lambda: 9], [])
        self.assertEqual(state["x"], (1, 2))

    def test_setter_evaluates_receiver_indices_value_once_without_getter(self):
        box, trace = {"value": 0}, []
        setter_assignment([(lambda: box, (lambda: 2, lambda: 3))], lambda: 7,
                          trace, lambda obj, i, j, v: obj.update(value=i+j+v))
        self.assertEqual(box["value"], 12)
        self.assertEqual(trace, ["receiver", ("index", 0), ("index", 1), "rhs", "setter"])

    def test_setter_multi_assignment_rejected_without_effects(self):
        box, trace = {"value": 0}, []
        with self.assertRaises(ResolutionError):
            setter_assignment([(lambda: box, (lambda: 0,))] * 2, lambda: 7,
                              trace, lambda obj, i, v: obj.update(value=v))
        self.assertEqual((box, trace), ({"value": 0}, []))

    def test_setter_frame_failure_does_not_call_method(self):
        box, trace = {"value": 0}, []
        with self.assertRaises(FrameError):
            setter_assignment([(lambda: box, (lambda: 0,))], lambda: 7, trace,
                              lambda obj, i, v: obj.update(value=v), permitted=False)
        self.assertEqual(box["value"], 0)
        self.assertNotIn("setter", trace)

    def test_chained_statements_are_explicitly_sequential(self):
        state = {"x": (1, 2)}
        pure_assignment([indexed(state, "x", 0)], [lambda: 7], [])
        pure_assignment([indexed(state, "x", 1)], [lambda: state["x"][0]+1], [])
        self.assertEqual(state["x"], (7, 8))


if __name__ == "__main__":
    unittest.main(verbosity=1)
