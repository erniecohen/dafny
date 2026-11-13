#!/usr/bin/env python3
"""Ground consistency witnesses for the exact finite facts in the Q1 sources.

Standard library only. This is a deliberately small, fail-closed translator for
the expressions in these design probes, not a Dafny translator. All assume-axiom
expressions are read from the input files; none is replaced by a trusted value.
Only the checked reference-function certificates become finite ground values.
The resulting SAT claim concerns this new-fact slice, not the Dafny prelude.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

M = 2 ** 64
ORDER = ["A", "C", "L", "R", "add", "sub", "mul", "div", "mod", "le"]
EXPECTED = {"control_" + s + "64": 4 if s in ("L", "R") else 2 for s in ORDER}
TOKEN = re.compile(r"\s*(<==>|==>|&&|\|\||<=|>=|==|!=|<<|>>|[A-Za-z_]\w*|\d+|[(),!+*/%&|^<>=-])")
PREC = {"<==>": 1, "==>": 2, "||": 3, "&&": 4,
        "==": 5, "!=": 5, "<=": 5, "<": 5, ">=": 5, ">": 5,
        "|": 6, "^": 7, "&": 8, "<<": 9, ">>": 9,
        "+": 10, "-": 10, "*": 11, "/": 11, "%": 11, "as": 13}
CMP = {"==", "!=", "<=", "<", ">=", ">"}


def norm(s):
    return re.sub(r"\s+", "", s)


def sha(s):
    return hashlib.sha256(s.encode()).hexdigest()


class Expression:
    def __init__(self, text):
        self.tokens, pos = [], 0
        text = text.strip()
        while pos < len(text):
            m = TOKEN.match(text, pos)
            if not m:
                raise ValueError("unsupported expression near " + repr(text[pos:pos+50]))
            self.tokens.append(m[1])
            pos = m.end()
        self.i, self.observations = 0, set()

    def peek(self):
        return self.tokens[self.i] if self.i < len(self.tokens) else None

    def take(self, expected=None):
        t = self.peek()
        if t is None or (expected is not None and t != expected):
            raise ValueError("expected " + repr(expected) + ", got " + repr(t))
        self.i += 1
        return t

    def binary(self, op, a, b):
        x, tx = a
        y, ty = b
        if op in ("<<", ">>"):
            assert tx == "bv64" and ty in ("int", "bv64")
            if ty == "int":
                y = "((_ int2bv 64) " + y + ")"
            return "(" + {"<<": "bvshl", ">>": "bvlshr"}[op] + " " + x + " " + y + ")", tx
        assert tx == ty, (op, tx, ty)
        if op in ("==", "!=", "<==>"):
            r = "(= " + x + " " + y + ")"
            return ("(not " + r + ")" if op == "!=" else r), "bool"
        if op in ("==>", "&&", "||"):
            assert tx == "bool"
            return "(" + {"==>": "=>", "&&": "and", "||": "or"}[op] + " " + x + " " + y + ")", "bool"
        if tx == "bv64":
            code = {"+": "bvadd", "-": "bvsub", "*": "bvmul", "/": "bvudiv", "%": "bvurem",
                    "&": "bvand", "|": "bvor", "^": "bvxor", "<=": "bvule", "<": "bvult",
                    ">=": "bvuge", ">": "bvugt"}[op]
        else:
            assert tx == "int"
            code = {"+": "+", "-": "-", "*": "*", "/": "div", "%": "mod",
                    "<=": "<=", "<": "<", ">=": ">=", ">": ">"}[op]
        r = "(" + code + " " + x + " " + y + ")"
        if tx == "bv64" and op in CMP:
            self.observations.add(r)
        return r, "bool" if op in CMP else tx

    def expr(self, minimum=0):
        t = self.take()
        if t == "(":
            a = self.expr()
            self.take(")")
        elif t in ("!", "-"):
            x, typ = self.expr(12)
            assert typ in (("bool", "bv64") if t == "!" else ("int",))
            a = "(" + ("-" if t == "-" else "bvnot" if typ == "bv64" else "not") + " " + x + ")", typ
        elif t.isdecimal():
            a = t, "int"
        elif t in ("true", "false"):
            a = t, "bool"
        elif t in ("p", "andNat"):
            self.take("(")
            args = [self.expr()]
            while self.peek() == ",":
                self.take(",")
                args.append(self.expr())
            self.take(")")
            assert len(args) == (1 if t == "p" else 3) and all(k == "int" for _, k in args)
            a = "(" + t + " " + " ".join(v for v, _ in args) + ")", "int"
            self.observations.add(a[0])
        else:
            raise ValueError("unsupported atom " + repr(t))
        while self.peek() in PREC and PREC[self.peek()] >= minimum:
            op = self.take()
            level = PREC[op]
            if op == "as":
                target = self.take()
                x, typ = a
                assert (typ, target) in (("int", "bv64"), ("bv64", "int"))
                a = ("((_ int2bv 64) " + x + ")", "bv64") if target == "bv64" else ("(bv2int " + x + ")", "int")
                if target == "int":
                    self.observations.add(a[0])
            elif op in CMP:
                # Dafny chained comparisons compare consecutive operands.
                b = self.expr(level + 1)
                parts = [self.binary(op, a, b)[0]]
                while self.peek() in CMP:
                    op = self.take()
                    c = self.expr(level + 1)
                    parts.append(self.binary(op, b, c)[0])
                    b = c
                a = (parts[0] if len(parts) == 1 else "(and " + " ".join(parts) + ")"), "bool"
            else:
                b = self.expr(level if op == "==>" else level + 1)
                a = self.binary(op, a, b)
        return a

    def parse(self):
        result, typ = self.expr()
        assert self.i == len(self.tokens) and typ == "bool"
        return result


def methods(text):
    text = re.sub(r"//[^\n]*", "", text)
    found = {}
    for m in re.finditer(r"(?ms)^lemma[^\n]*\b(control_\w+)\([^\n]*\).*?^\{\n(.*?)^\}", text):
        body = m[2]
        facts = re.findall(r"assume\s+\{:axiom\}\s+(.*?);", body, re.S)
        assert len(facts) == len(re.findall(r"\bassume\b", body))
        found[m[1]] = (body, facts)
    return found


def certificate_assertions(body, source):
    certs = []
    ensures = {norm(x) for x in re.findall(r"(?m)^\s*ensures\s+([^\n]+)$", source)}
    if "and53(64);" in norm(body):
        assert "andNat(n,5,3)==1" in ensures
        assert "requires3<=n" in norm(source)
        # Independent quotient/remainder calculation; no bitwise operator.
        a, b, place, answer = 5, 3, 1, 0
        for _ in range(64):
            answer += place * int(a % 2 == 1 and b % 2 == 1)
            a, b, place = a // 2, b // 2, place * 2
        assert answer == 1
        certs += [("certificate_and53_guard", "(<= 3 64)"),
                  ("certificate_and53", "(= (andNat 64 5 3) 1)")]
    if "powers();" in norm(body):
        assert "p(0)==1&&p(1)==2" in ensures
        assert "p(64)==" + str(M) in ensures
        certs += [("certificate_p" + str(n), "(= (p " + str(n) + ") " + str(2**n) + ")") for n in (0, 1, 64)]
    return certs


def make_case(name, body, facts, source, mutate=False):
    lines = ["; Finite new-fact consistency slice; not the full Dafny prelude.",
             "; Every assume-axiom expression is translated, preserving its guard.",
             "(set-logic ALL)", "(set-option :produce-models true)",
             "(set-option :produce-unsat-cores true)", "(set-option :rlimit 200000)",
             "(set-option :timeout 30000)", "(set-option :smt.random_seed 0)",
             "(set-option :sat.random_seed 0)", "(declare-fun p (Int) Int)",
             "(declare-fun andNat (Int Int Int) Int)"]
    certs = certificate_assertions(body, source)
    if "combined97" in name:
        # The inhabited source-lambda observation only. No higher-order/prelude axioms.
        lines += ["; Ground observation of source lambda t => t, requires 0 <= t, at t=0.",
                  "(define-fun t () Int 0)", "(define-fun domainRequires_t () Bool (<= 0 t))",
                  "(assert (! domainRequires_t :named lambda_precondition_at_zero))",
                  "(assert (! (<= 0 t) :named lambda_necessary_direction_at_zero))"]
    for label, expression in certs:
        lines.append("(assert (! " + expression + " :named " + label + "))")
    observations, mutated = set(), 0
    for i, original in enumerate(facts):
        expression = original
        if mutate and "andNat(" in norm(original):
            expression = original + "+1"
            mutated += 1
        parser = Expression(expression)
        smt = parser.parse()
        observations.update(parser.observations)
        lines += ["; source: " + " ".join(original.split()) + (" [MUTANT RHS +1]" if expression != original else ""),
                  "(assert (! " + smt + " :named bridge_" + str(i).zfill(2) + "))"]
    assert mutated == (2 if mutate else 0)
    lines += ["(check-sat)", "(get-info :reason-unknown)", "(get-info :all-statistics)"]
    if mutate:
        lines.append("(get-unsat-core)")
    elif observations:
        lines.append("(get-value (" + " ".join(sorted(observations)) + "))")
    query = "\n".join(lines) + "\n"
    assert "(forall" not in query and "(exists" not in query
    return query, len(certs)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--controls", type=Path, required=True)
    ap.add_argument("--lambda-controls", type=Path, required=True)
    ap.add_argument("--z3", action="append", required=True, help="repeat for both supported Z3 executables")
    ap.add_argument("--out", type=Path, required=True)
    args = ap.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    source, lam = args.controls.read_text(), args.lambda_controls.read_text()
    controls, combined = methods(source), methods(lam)
    assert set(controls) == set(EXPECTED)
    for name, count in EXPECTED.items():
        assert len(controls[name][1]) == count
        assert list(map(norm, controls[name][1])) == list(map(norm, combined[name][1]))
    name = "control_combined97_64"
    assert set(combined) == set(EXPECTED) | {name}
    assert "constdomain:=(t:int)requires0<=t=>t" in norm(lam)
    assert "requiresdomain.requires(t)" in norm(lam)
    assert "assert0<=t;" in norm(combined[name][0]) and "ift==0{" in norm(combined[name][0])
    all_facts = [fact for s in ORDER for fact in controls["control_" + s + "64"][1]]
    assert list(map(norm, combined[name][1])) == list(map(norm, all_facts))
    assert len(all_facts) == 24
    jobs = [(n, *controls[n], source, False) for n in EXPECTED]
    jobs += [(name, *combined[name], lam, False),
             ("mutant_A64", *controls["control_A64"], source, True),
             ("mutant_combined97_64", *combined[name], lam, True)]
    rows = []
    for name, body, facts, src, mutant in jobs:
        query, cert_count = make_case(name, body, facts, src, mutant)
        (args.out / (name + ".smt2")).write_text(query)
        for solver in args.z3:
            version_text = subprocess.check_output([solver, "--version"], text=True).strip()
            version = re.search(r"\d+\.\d+\.\d+", version_text)[0]
            result = subprocess.run([solver, str(args.out / (name + ".smt2"))],
                                    capture_output=True, text=True, timeout=40)
            tag = name + "-z3-" + version
            (args.out / (tag + ".out")).write_text(result.stdout)
            (args.out / (tag + ".err")).write_text(result.stderr)
            actual = result.stdout.splitlines()[0] if result.stdout else "no output"
            expected = "unsat" if mutant else "sat"
            ru = int(re.search(r":rlimit-count\s+(\d+)", result.stdout)[1])
            assert result.returncode == 0 and "(error" not in result.stdout and not result.stderr
            assert actual == expected and ru < 200000, (name, version, actual, ru)
            rows.append({"case": name, "solver": version, "result": actual,
                         "bridge_instances": len(facts), "certificates": cert_count,
                         "ru": ru, "query_sha256": sha(query)})
    report = {"scope": "finite new-fact slice, not the full Dafny prelude or #97 encoding",
              "source_sha256": {args.controls.name: sha(source), args.lambda_controls.name: sha(lam)},
              "all_source_bridge_instances_retained": True,
              "combined_bridge_instances": 24, "results": rows}
    (args.out / "ground-results.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"pass": True, "solver_checks": len(rows), "sat": sum(r["result"] == "sat" for r in rows),
                      "unsat_mutants": sum(r["result"] == "unsat" for r in rows), "max_ru": max(r["ru"] for r in rows)}))


if __name__ == "__main__":
    main()
