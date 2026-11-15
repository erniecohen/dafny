#!/usr/bin/env python3
"""Issue 261: executable miniature semantics, not a model of Dafny's AST.

The cold oracle does not use cache keys. Tests compare warm outputs against that
oracle and compare misses against independently recorded complete input bytes.
Run: python3 model_module_dag.py
"""

from dataclasses import asdict, dataclass, replace
import hashlib
import json
import random
import unittest


def encode(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def digest(tag, value):
    return hashlib.sha256(encode([tag, value])).hexdigest()


@dataclass(frozen=True)
class Module:
    name: str
    imports: tuple = ()               # (alias, target) pairs, in source order
    public: int = 1                    # exported contract
    transparent: int = 2              # body visible to importers
    hidden: int = 0                   # opaque implementation/proof
    bodies: tuple = (("f", 1), ("g", 2))
    reject_whole_program: bool = False
    extends_traits: tuple = ()        # downstream declarations opening a trait
    calls_traits: tuple = ()          # global-analysis queries used by translation


@dataclass(frozen=True)
class Resolved:
    public: int
    transparent: int
    tasks: tuple


def resolve_semantics(module, prerequisites, engine):
    """Small language semantics; dependency implementation text is invisible."""
    visible = tuple((alias, prerequisites[target].public,
                     prerequisites[target].transparent)
                    for alias, target in module.imports)
    # Both supported engines agree on meaning; identity remains a resolution key.
    assert engine in ("legacy", "refreshed")
    tasks = tuple(encode([module.name, name, value, visible])
                  for name, value in module.bodies)
    return Resolved(module.public, module.transparent, tasks)


def cold_oracle(modules, engine="legacy"):
    """Independent interpreter: recursive imports, no hashing/caching/scheduler."""
    resolved = {}
    visiting = set()

    def evaluate(name):
        if name in resolved:
            return resolved[name]
        if name in visiting:
            raise ValueError("cyclic imports")
        visiting.add(name)
        module = modules[name]
        inputs = {target: evaluate(target) for _, target in module.imports}
        # Deliberately construct tasks here instead of calling cached resolver.
        visible = [(alias, inputs[target].public, inputs[target].transparent)
                   for alias, target in module.imports]
        tasks = tuple(encode([name, label, value, visible])
                      for label, value in module.bodies)
        resolved[name] = Resolved(module.public, module.transparent, tasks)
        visiting.remove(name)
        return resolved[name]

    for name in sorted(modules):
        evaluate(name)
    # Whole-program classification can change an earlier module's translation
    # without changing its local resolution inputs or ordinary import DAG.
    opened = {trait for module in modules.values() for trait in module.extends_traits}
    final = {}
    for name, module in modules.items():
        local = resolved[name]
        checks = sorted(set(module.calls_traits) & opened)
        tasks = tuple(encode([json.loads(task), ["strict-bound", checks]])
                      if checks else task for task in local.tasks)
        final[name] = Resolved(local.public, local.transparent, tasks)
    return final, any(m.reject_whole_program for m in modules.values())


def solve_model(task):
    """Deterministic finite mock solver; this does not call/validate Z3."""
    integer = int.from_bytes(hashlib.sha256(task).digest(), "big")
    return ("Errors" if integer % 13 == 0 else "Correct", len(task) * 17)


class Cache:
    def __init__(self, observable=False):
        self.observable = observable
        self.modules = {}
        self.tasks = {}
        self.last_keys = {}
        self.last_inputs = {}
        self.last_final_keys = {}
        self.translations = {}
        self.translator_calls = 0
        self.module_calls = self.solver_calls = 0

    def run(self, modules, engine="legacy", builtins=("int", "bool"),
            options=("default",), version="v1"):
        self.module_calls = self.solver_calls = self.translator_calls = 0
        self.last_keys, self.last_inputs, self.last_final_keys = {}, {}, {}
        resolved, pending = {}, set(modules)
        # A different evaluation strategy from the oracle: Kahn-style scheduler.
        while pending:
            ready = sorted(name for name in pending if all(
                target in resolved for _, target in modules[name].imports))
            if not ready:
                raise ValueError("cyclic imports")
            for name in ready:
                module = modules[name]
                edges = []
                for slot, (alias, target) in enumerate(module.imports):
                    dependency = resolved[target]
                    value = (digest("visible/v1", [dependency.public,
                                                  dependency.transparent])
                             if self.observable else self.last_keys[target])
                    edges.append((slot, alias, target, value))
                envelope = [version, engine, builtins, options,
                            asdict(module), edges]
                preimage = encode(["resolution/v1", envelope])
                key = hashlib.sha256(preimage).hexdigest()
                self.last_keys[name], self.last_inputs[name] = key, preimage
                entry = self.modules.get(key)
                # Corrupt stored bytes fail closed before producing a result.
                if entry is not None:
                    payload, checksum = entry
                    if checksum != hashlib.sha256(payload).hexdigest():
                        entry = None
                if entry is None:
                    self.module_calls += 1
                    result = resolve_semantics(module, resolved, engine)
                    payload = encode([result.public, result.transparent,
                                      [task.hex() for task in result.tasks]])
                    self.modules[key] = (payload,
                                         hashlib.sha256(payload).hexdigest())
                else:
                    public, transparent, tasks = json.loads(entry[0])
                    result = Resolved(public, transparent,
                                      tuple(bytes.fromhex(t) for t in tasks))
                resolved[name] = result
                pending.remove(name)
        # L identifies local resolution. G is a query projection of current
        # whole-program facts. Finalized R=H(L,G) is the literal translation key.
        # No finalized R recursively appears in another module's local L key.
        global_open = set().union(*(set(m.extends_traits) for m in modules.values()))
        final = {}
        for name, result in resolved.items():
            queries = tuple((trait, trait in global_open)
                            for trait in sorted(set(modules[name].calls_traits)))
            rkey = digest("final-resolution/v1", [self.last_keys[name], queries])
            self.last_final_keys[name] = rkey
            checks = [trait for trait, opened in queries if opened]
            # The model records the finalized key even on rejected programs but
            # does not publish translation/proof results before admission.
            final[name] = Resolved(result.public, result.transparent, tuple(
                encode([json.loads(task), ["strict-bound", checks]]) if checks else task
                for task in result.tasks))
        # Admission is a barrier: no cached or fresh proof result is reported for
        # a program rejected by the current whole-program pass.
        rejected = any(m.reject_whole_program for m in modules.values())
        if rejected:
            return final, {}, True
        for name in sorted(final):
            rkey = self.last_final_keys[name]
            if rkey not in self.translations:
                self.translator_calls += 1
                self.translations[rkey] = final[name]
            # A stored BPL stand-in is authoritative only under finalized R.
            final[name] = self.translations[rkey]
        resolved = final
        answers = {}
        for name in sorted(resolved):
            rows = []
            for task in resolved[name].tasks:
                key = digest("task/v1", ["solver-image-1", "seed-0", task.hex()])
                if key not in self.tasks:
                    self.solver_calls += 1
                    self.tasks[key] = solve_model(task)
                rows.append(self.tasks[key])
            answers[name] = tuple(rows)
        return resolved, answers, False


class Tests(unittest.TestCase):
    @staticmethod
    def sample():
        return {"A": Module("A"),
                "B": Module("B", (("a", "A"),)),
                "C": Module("C", (("b", "B"),)),
                "U": Module("U")}

    def check_oracle(self, cache, modules, **kwargs):
        warm, rows, rejected = cache.run(modules, **kwargs)
        cold, expected_rejected = cold_oracle(modules, kwargs.get("engine", "legacy"))
        self.assertEqual(cold, warm)
        self.assertEqual(expected_rejected, rejected)
        expected_rows = {} if rejected else {name: tuple(map(solve_model, m.tasks))
                                              for name, m in cold.items()}
        self.assertEqual(expected_rows, rows)

    def test_unchanged_zero_solver_and_resolver_calls(self):
        modules, cache = self.sample(), Cache()
        self.check_oracle(cache, modules)
        self.assertEqual((4, 8), (cache.module_calls, cache.solver_calls))
        self.check_oracle(cache, modules)
        self.assertEqual((0, 0), (cache.module_calls, cache.solver_calls))

    def test_one_function_edit_only_one_task_changes(self):
        modules, cache = self.sample(), Cache()
        self.check_oracle(cache, modules)
        modules["B"] = replace(modules["B"], bodies=(("f", 99), ("g", 2)))
        self.check_oracle(cache, modules)
        self.assertEqual((2, 1), (cache.module_calls, cache.solver_calls))

    def test_dependency_edit_early_cutoff(self):
        modules, cache = self.sample(), Cache()
        self.check_oracle(cache, modules)
        old_keys = cache.last_keys.copy()
        modules["A"] = replace(modules["A"], hidden=17)
        self.check_oracle(cache, modules)
        self.assertEqual((3, 0), (cache.module_calls, cache.solver_calls))
        self.assertNotEqual(old_keys["B"], cache.last_keys["B"])
        self.assertEqual(old_keys["U"], cache.last_keys["U"])

    def test_observable_projection_skips_dependent_resolution(self):
        modules, cache = self.sample(), Cache(observable=True)
        self.check_oracle(cache, modules)
        modules["A"] = replace(modules["A"], hidden=17)
        self.check_oracle(cache, modules)
        self.assertEqual((1, 0), (cache.module_calls, cache.solver_calls))
        modules["A"] = replace(modules["A"], transparent=31)
        self.check_oracle(cache, modules)
        self.assertEqual((2, 2), (cache.module_calls, cache.solver_calls))

    def test_alias_binding_and_source_order_are_inputs(self):
        modules = self.sample()
        modules["D"] = Module("D", (("x", "A"), ("y", "U")))
        cache = Cache()
        self.check_oracle(cache, modules)
        old = cache.last_keys["D"]
        modules["D"] = replace(modules["D"], imports=(("y", "U"), ("x", "A")))
        self.check_oracle(cache, modules)
        self.assertNotEqual(old, cache.last_keys["D"])
        self.assertEqual((1, 2), (cache.module_calls, cache.solver_calls))

    def test_global_admission_reruns_on_module_hits(self):
        modules, cache = self.sample(), Cache()
        self.check_oracle(cache, modules)
        modules["V"] = Module("V", reject_whole_program=True)
        self.check_oracle(cache, modules)
        self.assertEqual(1, cache.module_calls)
        self.assertEqual(0, cache.solver_calls)
        self.assertTrue(cache.run(modules)[2])
        self.assertEqual((0, 0), (cache.module_calls, cache.solver_calls))

    def test_engine_options_builtins_and_version(self):
        for changed in ({"engine": "refreshed"}, {"options": ("changed",)},
                        {"builtins": ("int", "bool", "bv3")}, {"version": "v2"}):
            with self.subTest(changed=changed):
                modules, cache = self.sample(), Cache()
                self.check_oracle(cache, modules)
                self.check_oracle(cache, modules, **changed)
                self.assertEqual((4, 0), (cache.module_calls, cache.solver_calls))

    def test_fresh_process_uses_identical_key_bytes(self):
        modules, first, second = self.sample(), Cache(), Cache()
        self.check_oracle(first, modules)
        # Only serialized tables cross the process boundary; no AST objects.
        second.modules = first.modules.copy()
        second.tasks = first.tasks.copy()
        second.translations = first.translations.copy()
        self.check_oracle(second, dict(reversed(list(modules.items()))))
        self.assertEqual(first.last_keys, second.last_keys)
        self.assertEqual((0, 0), (second.module_calls, second.solver_calls))

    def test_corruption_is_miss(self):
        modules, cache = self.sample(), Cache()
        self.check_oracle(cache, modules)
        key = cache.last_keys["B"]
        payload, checksum = cache.modules[key]
        cache.modules[key] = (payload + b"corrupt", checksum)
        self.check_oracle(cache, modules)
        self.assertEqual((1, 0), (cache.module_calls, cache.solver_calls))

    def test_key_framing_and_tuple_lengths(self):
        self.assertNotEqual(digest("x", ["ab", "c"]), digest("x", ["a", "bc"]))
        self.assertNotEqual(digest("tuple", [False, False]),
                            digest("tuple", [False, False, False]))

    def test_cycles_rejected_before_reuse(self):
        modules, cache = self.sample(), Cache()
        self.check_oracle(cache, modules)
        modules["A"] = replace(modules["A"], imports=(("c", "C"),))
        with self.assertRaises(ValueError):
            cache.run(modules)
        with self.assertRaises(ValueError):
            cold_oracle(modules)

    def test_downstream_extension_changes_final_key_not_local_key(self):
        modules, cache = self.sample(), Cache()
        modules["A"] = replace(modules["A"], calls_traits=("A.T",))
        self.check_oracle(cache, modules)
        old_local = cache.last_keys["A"]
        old_final = cache.last_final_keys["A"]
        before, _ = cold_oracle(modules)
        # D imports A, but A does not import D: ordinary source edges cannot
        # explain why adding D changes the global-dispatch checks emitted in A.
        modules["D"] = Module("D", (("a", "A"),), extends_traits=("A.T",))
        self.check_oracle(cache, modules)
        after, _ = cold_oracle(modules)
        self.assertEqual(old_local, cache.last_keys["A"])
        self.assertNotEqual(old_final, cache.last_final_keys["A"])
        self.assertNotEqual(before["A"].tasks, after["A"].tasks)
        self.assertEqual(1, cache.module_calls)
        self.assertEqual(2, cache.translator_calls)  # changed A and new D
        self.assertEqual(4, cache.solver_calls)

    def test_irrelevant_global_fact_does_not_change_final_key(self):
        modules, cache = self.sample(), Cache()
        modules["A"] = replace(modules["A"], calls_traits=("A.T",))
        self.check_oracle(cache, modules)
        old_local = cache.last_keys.copy()
        old_final = cache.last_final_keys.copy()
        before, _ = cold_oracle(modules)
        modules["D"] = Module("D", extends_traits=("Other.T",))
        self.check_oracle(cache, modules)
        after, _ = cold_oracle(modules)
        for name in old_local:
            self.assertEqual(old_local[name], cache.last_keys[name])
            self.assertEqual(old_final[name], cache.last_final_keys[name])
            self.assertEqual(before[name], after[name])
        self.assertEqual((1, 1, 2), (cache.module_calls,
                                    cache.translator_calls, cache.solver_calls))

    def test_random_edit_sequences_with_independent_hit_oracle(self):
        rng = random.Random(261)
        for mode in (False, True):
            cache, seen_inputs, seen_tasks = Cache(mode), set(), set()
            modules = self.sample()
            for step in range(200):
                name = rng.choice(sorted(modules))
                field = rng.choice(("public", "transparent", "hidden"))
                modules[name] = replace(modules[name], **{field: rng.randrange(30)})
                self.check_oracle(cache, modules)
                expected_misses = sum(value not in seen_inputs
                                      for value in cache.last_inputs.values())
                self.assertEqual(expected_misses, cache.module_calls)
                seen_inputs.update(cache.last_inputs.values())
                cold, _ = cold_oracle(modules)
                current_tasks = {t for m in cold.values() for t in m.tasks}
                self.assertEqual(len(current_tasks - seen_tasks), cache.solver_calls)
                seen_tasks.update(current_tasks)


if __name__ == "__main__":
    unittest.main(verbosity=2)
