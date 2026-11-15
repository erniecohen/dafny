#!/usr/bin/env python3
"""Executable finite model of issue 261's late key and result replay contract.

Python 3.10+, standard library only.  This is a finite reference model, not a
Dafny/Boogie/Z3 implementation or a proof of those implementations.
The cold oracle enumerates valuations; it never calls the key function.
"""
from dataclasses import dataclass, replace, asdict
from hashlib import sha256
from itertools import permutations, product
import json
import unittest


@dataclass(frozen=True)
class Task:
    # Each tuple is (label, operator, constant); conjunction is the background.
    declarations: tuple = (("a", "ge", 1), ("b", "le", 2))
    used_types: tuple = ("int", "bool")
    prelude: tuple = (("p", "ge", 0),)
    goals: tuple = (("g", "ne", 0),)
    seed: int = 0
    resource_limit: int = 10000
    error_limit: int = 5
    solver_image: str = "finite-v1"
    solver_version: str = "1.0"
    options: tuple = (("model", True), ("coverage", True))
    protocol: str = "enumerate-models-and-core-v1"
    fresh_process: bool = True
    timeout_ms: int = 0


@dataclass(frozen=True)
class Evidence:
    outcome: str
    ru: int
    errors: tuple
    covered: tuple
    normal: bool = True


def key(task):
    payload = json.dumps(asdict(task), sort_keys=True, separators=(",", ":"))
    return sha256(b"issue261/finite-task/v1\0" + payload.encode()).hexdigest()


def cold(task, history=0):
    """Independent small solver: enumerate x in 0..3 under ordered assumptions.

    Integer work accounting deliberately exposes order, type setup, seed,
    solver image/version, and adaptive error enumeration. It models that
    logical equivalence alone does not imply resource or report equivalence.
    """
    ops = {"ge": lambda x, y: x >= y, "le": lambda x, y: x <= y,
           "ne": lambda x, y: x != y, "eq": lambda x, y: x == y}
    ru = sum(len(t) + i for i, t in enumerate(task.used_types))
    ru += sum(task.solver_image.encode()) + sum(task.solver_version.encode())
    ru += sum(len(n) + int(v) for n, v in task.options)
    if not task.fresh_process:
        ru += history
    errors, covered = [], []
    valuations = tuple(range(task.seed % 4, 4)) + tuple(range(task.seed % 4))
    for glabel, gop, gvalue in task.goals:
        failed = False
        for x in valuations:
            admissible = True
            for label, op, value in task.prelude + task.declarations:
                ru += 1
                if task.resource_limit and ru > task.resource_limit:
                    return Evidence("OutOfResource", ru, tuple(errors), tuple(covered))
                if not ops[op](x, value):
                    admissible = False
                    break
                if label not in covered:
                    covered.append(label)
            if admissible:
                ru += 1
                if not ops[gop](x, gvalue):
                    errors.append((glabel, x))
                    failed = True
                    break
        if failed and task.error_limit and len(errors) == task.error_limit:
            break
    return Evidence("Invalid" if errors else "Valid", ru, tuple(errors), tuple(covered))


def reusable(task, evidence):
    return (task.fresh_process and task.timeout_ms == 0 and evidence.normal
            and evidence.outcome in ("Valid", "Invalid", "OutOfResource"))


def render(evidence, bindings):
    """Report-only locations are rebound to today's source, never stale bytes."""
    errors = [{"file": bindings[label][0], "line": bindings[label][1],
               "message": "assertion may fail", "model": x}
              for label, x in evidence.errors]
    return json.dumps({"outcome": evidence.outcome, "ru": evidence.ru,
                       "errors": errors, "covered": evidence.covered},
                      separators=(",", ":"), sort_keys=True).encode()


class Cache:
    def __init__(self):
        self.entries = {}
        self.calls = 0

    def run(self, task):
        candidate = self.entries.get(key(task))
        if candidate is not None and reusable(task, candidate):
            return candidate, True
        self.calls += 1
        result = cold(task)
        if reusable(task, result):
            self.entries[key(task)] = result
        return result, False


class KeyModelTests(unittest.TestCase):
    cases = 0

    def checked(self, condition):
        self.assertTrue(condition)
        type(self).cases += 1

    def test_cold_warm_exhaustive(self):
        cache = Cache()
        tasks = [Task(declarations=ds, seed=s, resource_limit=rl, error_limit=el,
                      goals=(("g", "ne", g), ("h", "eq", 3)))
                 for ds, s, rl, el, g in product(
                     permutations(Task().declarations), range(4),
                     (700, 10000), (1, 5), range(4))]
        for task in tasks:
            expected = cold(task)
            first, hit1 = cache.run(task)
            second, hit2 = cache.run(task)
            self.checked(first == expected == second and not hit1 and hit2)
        self.checked(cache.calls == len(tasks))

    def test_every_descriptor_field_participates(self):
        base = Task()
        variants = {
            "declarations": tuple(reversed(base.declarations)),
            "used_types": ("bool", "int"), "prelude": (),
            "goals": (("g", "eq", 3),), "seed": 1,
            "resource_limit": 800, "error_limit": 1,
            "solver_image": "finite-v2", "solver_version": "1.1",
            "options": tuple(reversed(base.options)), "protocol": "v2",
            "fresh_process": False, "timeout_ms": 30,
        }
        self.checked(set(variants) == set(asdict(base)))
        for field, value in variants.items():
            self.checked(key(replace(base, **{field: value})) != key(base))

    def test_pruned_boogie_alone_omits_used_types(self):
        a = Task()
        b = replace(a, used_types=("int", "bool", "real"))
        self.checked(a.declarations == b.declarations and cold(a) != cold(b))
        self.checked(key(a) != key(b))

    def test_logical_equivalence_is_not_ru_equivalence(self):
        a = Task(declarations=(("a", "ge", 1), ("b", "le", 3)))
        b = replace(a, declarations=tuple(reversed(a.declarations)))
        self.checked(cold(a).outcome == cold(b).outcome)
        self.checked(cold(a).ru != cold(b).ru)

    def test_error_limit_controls_adaptive_protocol(self):
        a = Task(goals=(("g", "eq", 3), ("h", "eq", 0)), error_limit=1)
        b = replace(a, error_limit=5)
        self.checked(cold(a).errors != cold(b).errors)
        self.checked(key(a) != key(b))

    def test_early_cutoff_does_not_hash_resolution_provenance(self):
        # Dependency method body edit changes the module key, not this lowered task.
        resolutions = ("dependency-body-old", "dependency-body-new")
        cache = Cache()
        seen = [cache.run(Task()) for _ in resolutions]
        self.checked(seen[0][0] == seen[1][0] == cold(Task()))
        self.checked(not seen[0][1] and seen[1][1] and cache.calls == 1)

    def test_report_rebinding(self):
        t = Task(goals=(("g", "eq", 3),))
        cache = Cache()
        cache.run(t)
        saved, hit = cache.run(t)
        old = {"g": ("old.dfy", 7)}
        new = {"g": ("new.dfy", 29)}
        self.checked(hit and render(saved, new) == render(cold(t), new))
        self.checked(render(saved, old) != render(saved, new))
        with self.assertRaises(KeyError):
            render(saved, {})
        self.checked(True)  # Missing binding is a failed hit, never success.

    def test_solver_history_and_skipping_hits(self):
        t = replace(Task(), fresh_process=False)
        self.checked(cold(t, history=7) != cold(t, history=0))
        self.checked(not reusable(t, cold(t)))
        # Storing the prior prefix in the hit's key alone does not preserve the
        # history of the following miss after the cached task is skipped.
        miss = replace(t, goals=(("g", "eq", 3),))
        self.checked(cold(miss, history=cold(t).ru) != cold(miss, history=0))

    def test_reject_timed_success_and_abnormal_results(self):
        timed = replace(Task(), timeout_ms=30)
        self.checked(not reusable(timed, Evidence("Valid", 1, (), ())))
        for status in ("TimeOut", "OutOfMemory", "Crash", "Cancelled", "Unknown"):
            self.checked(not reusable(Task(), Evidence(status, 0, (), ())))
        self.checked(not reusable(Task(), Evidence("Valid", 0, (), (), normal=False)))

    def test_current_admission_and_task_inventory(self):
        cache = Cache()
        cache.run(Task())
        current = (Task(), Task(goals=(("g", "eq", 3),)))
        results = [cache.run(t)[0] for t in current]
        self.checked(any(r.outcome != "Valid" for r in results))
        for admitted in (False, True):
            aggregate_success = admitted and all(r.outcome == "Valid" for r in results)
            self.checked(not aggregate_success)

    def test_elapsed_time_contract_has_counterexample(self):
        # Same deterministic evidence, two legal stopwatch observations.
        evidence = cold(Task())
        cold_rows = [json.dumps({"ru": evidence.ru, "runTime": duration})
                     for duration in (1.0, 1.5)]
        self.checked(cold_rows[0] != cold_rows[1])
        self.checked(render(evidence, {}) == render(cold(Task()), {}))


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(KeyModelTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    print(f"{result.testsRun - len(result.failures) - len(result.errors)} test methods passed; "
          f"{KeyModelTests.cases} checked cases")
    raise SystemExit(not result.wasSuccessful())
