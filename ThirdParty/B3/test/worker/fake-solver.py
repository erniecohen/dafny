#!/usr/bin/env python3
"""Deterministic protocol faults, not a mathematical solver."""
import sys
import time

scenario = sys.argv[1]
assertion_seen = False
for raw in sys.stdin:
    command = raw.strip()
    if scenario == "silent":
        time.sleep(10)
        sys.exit(0)
    if scenario == "oversized":
        print("x" * 1048577, flush=True)
        continue
    if scenario == "immediate-eof":
        sys.exit(0)
    if scenario == "stderr-flood":
        sys.stderr.write("x" * 65536)
        sys.stderr.flush()
    if command == "(exit)":
        sys.exit(0)
    if command.startswith("(assert "):
        assertion_seen = True
        if scenario == "bad-assert":
            print('(error "assertion was rejected")', flush=True)
            continue
    if command == "(pop)" and scenario == "bad-pop":
        print('(error "pop was rejected")', flush=True)
    elif command == "(push)" and scenario == "bad-push":
        print('(error "push was rejected")', flush=True)
    elif command.startswith("(set-option ") and scenario == "bad-option":
        print("unsupported", flush=True)
    elif command == "(check-sat)":
        if scenario in ("unknown", "unknown-error"):
            print("unknown", flush=True)
        elif scenario == "sat":
            print("sat", flush=True)
        elif scenario == "invalid-answer":
            print("certainly-unsat", flush=True)
        elif scenario == "eof-query":
            sys.exit(0)
        elif scenario == "unread-error":
            print('(error "earlier operation failed")\nunsat', flush=True)
        else:
            print("unsat", flush=True)
    elif command == "(get-info :reason-unknown)":
        if scenario == "unknown-error":
            print('( \n error "reason failed")', flush=True)
        else:
            print('(:reason-unknown "deterministic fixture")', flush=True)
    elif command == "(get-model)":
        if scenario == "truncated-model":
            print('(model (define-fun x () Int 1)', flush=True)
            sys.exit(0)
        else:
            print('(model (define-fun |x(y)| () String "(a) ""b"""))', flush=True)
    else:
        print("success", flush=True)
