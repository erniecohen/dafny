#!/usr/bin/env python3
"""Small executable storage specification for Dafny issue 261; standard library only.

This models trusted local producers, immutable records, atomic no-clobber publication,
integrity checks, races, and eviction-as-miss. It is not a production cache or a proof
against malicious writers. Run: python3 model_storage.py
"""
from __future__ import annotations

import hashlib
import multiprocessing
import os
from pathlib import Path
import struct
import tempfile
import unittest

MAGIC = b"DFYC261\0"
VERSION = 1
MAX_BYTES = 1 << 20
HEADER = struct.Struct(">8sII32s32sQ")
KINDS = {"resolution": 1, "translation": 2, "task": 3}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def key(data: bytes) -> str:
    return digest(b"261-model-input\0" + len(data).to_bytes(8, "big") + data)


def encode(kind: str, input_key: str, payload: bytes) -> bytes:
    return HEADER.pack(MAGIC, VERSION, KINDS[kind], bytes.fromhex(input_key),
                       hashlib.sha256(payload).digest(), len(payload)) + payload


def decode(kind: str, input_key: str, raw: bytes) -> bytes | None:
    if len(raw) < HEADER.size or len(raw) > HEADER.size + MAX_BYTES:
        return None
    magic, version, kind_id, stored_key, checksum, size = HEADER.unpack(raw[:HEADER.size])
    payload = raw[HEADER.size:]
    if (magic != MAGIC or version != VERSION or kind_id != KINDS[kind]
            or stored_key != bytes.fromhex(input_key) or size != len(payload)
            or hashlib.sha256(payload).digest() != checksum):
        return None
    return payload


class Store:
    def __init__(self, root: str | Path):
        self.root = Path(root)

    def path(self, kind: str, input_key: str) -> Path:
        if kind not in KINDS or len(input_key) != 64 or any(c not in "0123456789abcdef" for c in input_key):
            raise ValueError("invalid kind/key")
        return self.root / "v1" / kind / input_key[:2] / (input_key + ".entry")

    def get(self, kind: str, input_key: str) -> bytes | None:
        path = self.path(kind, input_key)
        poisoned = path.with_suffix(".poison")
        try:
            if poisoned.exists() or path.stat().st_size > MAX_BYTES + HEADER.size:
                return None
            with path.open("rb") as stream:
                raw = stream.read(MAX_BYTES + HEADER.size + 1)
            value = decode(kind, input_key, raw)
            return None if poisoned.exists() else value
        except (OSError, ValueError, struct.error):
            return None

    def put(self, kind: str, input_key: str, payload: bytes) -> bool:
        if len(payload) > MAX_BYTES:
            return False
        path = self.path(kind, input_key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.with_suffix(".poison").exists():
            return False
        raw = encode(kind, input_key, payload)
        fd, temporary = tempfile.mkstemp(prefix=".pending-", dir=path.parent)
        try:
            with os.fdopen(fd, "wb") as stream:
                stream.write(raw)
                stream.flush()
                os.fsync(stream.fileno())
            try:
                os.link(temporary, path)  # Atomic publication, never replaces a winner.
                directory = os.open(path.parent, os.O_RDONLY)
                try:
                    os.fsync(directory)
                finally:
                    os.close(directory)
                return not path.with_suffix(".poison").exists()
            except FileExistsError:
                if self.get(kind, input_key) == payload:
                    return True
                # Same declared input produced unequal bytes, or existing record is corrupt.
                # Conservatively disable this key; never bless a last-writer winner.
                path.with_suffix(".poison").touch(exist_ok=True)
                return False
        finally:
            os.unlink(temporary)


def publish_worker(root: str, input_key: str, payload: bytes) -> None:
    Store(root).put("task", input_key, payload)


class StorageTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.store = Store(self.directory.name)
        self.k = key(b"current exact task")
        self.value = b"Valid; RU=312; complete-obligation-map"

    def put(self):
        self.assertTrue(self.store.put("task", self.k, self.value))

    def test_01_absent_is_miss(self):
        self.assertIsNone(self.store.get("task", self.k))

    def test_02_round_trip(self):
        self.put()
        self.assertEqual(self.value, self.store.get("task", self.k))

    def test_03_second_process_view(self):
        self.put()
        self.assertEqual(self.value, Store(self.directory.name).get("task", self.k))

    def test_04_same_publication_idempotent(self):
        self.put()
        before = self.store.path("task", self.k).read_bytes()
        self.put()
        self.assertEqual(before, self.store.path("task", self.k).read_bytes())

    def test_05_conflict_poisoned(self):
        self.put()
        self.assertFalse(self.store.put("task", self.k, b"different answer"))
        self.assertIsNone(self.store.get("task", self.k))
        self.assertFalse(self.store.put("task", self.k, self.value))

    def test_06_truncation_is_miss(self):
        self.put()
        path = self.store.path("task", self.k)
        path.write_bytes(path.read_bytes()[:-1])
        self.assertIsNone(self.store.get("task", self.k))

    def test_07_checksum_is_checked(self):
        self.put()
        path = self.store.path("task", self.k)
        raw = path.read_bytes()
        path.write_bytes(raw[:-1] + bytes([raw[-1] ^ 1]))
        self.assertIsNone(self.store.get("task", self.k))

    def test_08_wrong_key_is_miss(self):
        self.put()
        path = self.store.path("task", self.k)
        path.write_bytes(encode("task", key(b"old task"), self.value))
        self.assertIsNone(self.store.get("task", self.k))

    def test_09_wrong_schema_is_miss(self):
        self.put()
        path = self.store.path("task", self.k)
        raw = bytearray(path.read_bytes())
        raw[8:12] = struct.pack(">I", VERSION + 1)
        path.write_bytes(raw)
        self.assertIsNone(self.store.get("task", self.k))

    def test_10_wrong_domain_is_miss(self):
        self.put()
        self.store.path("task", self.k).write_bytes(encode("resolution", self.k, self.value))
        self.assertIsNone(self.store.get("task", self.k))

    def test_11_namespaces_are_disjoint(self):
        self.put()
        self.assertIsNone(self.store.get("resolution", self.k))
        self.assertTrue(self.store.put("resolution", self.k, b"resolved graph"))
        self.assertEqual(self.value, self.store.get("task", self.k))

    def test_12_partial_temporary_file_invisible(self):
        path = self.store.path("task", self.k)
        path.parent.mkdir(parents=True)
        (path.parent / ".pending-abandoned").write_bytes(b"partial")
        self.assertIsNone(self.store.get("task", self.k))
        self.put()

    def test_13_eviction_is_miss(self):
        self.put()
        self.store.path("task", self.k).unlink()
        self.assertIsNone(self.store.get("task", self.k))
        self.put()

    def test_14_oversized_record_is_miss(self):
        self.assertFalse(self.store.put("task", self.k, b"x" * (MAX_BYTES + 1)))
        path = self.store.path("task", self.k)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(encode("task", self.k, b"x" * (MAX_BYTES + 1)))
        self.assertIsNone(self.store.get("task", self.k))

    def test_15_trailing_bytes_is_miss(self):
        self.put()
        path = self.store.path("task", self.k)
        path.write_bytes(path.read_bytes() + b"unconsumed")
        self.assertIsNone(self.store.get("task", self.k))

    def test_16_parallel_identical_writers(self):
        context = multiprocessing.get_context("spawn")
        workers = [context.Process(target=publish_worker,
                    args=(self.directory.name, self.k, self.value)) for _ in range(6)]
        for worker in workers:
            worker.start()
        for worker in workers:
            worker.join(15)
            self.assertEqual(0, worker.exitcode)
        self.assertEqual(self.value, self.store.get("task", self.k))

    def test_17_parallel_conflict_is_miss(self):
        context = multiprocessing.get_context("spawn")
        workers = [context.Process(target=publish_worker,
                    args=(self.directory.name, self.k, value))
                   for value in (self.value, b"inconsistent producer")]
        for worker in workers:
            worker.start()
        for worker in workers:
            worker.join(15)
            self.assertEqual(0, worker.exitcode)
        self.assertIsNone(self.store.get("task", self.k))

    def test_18_descriptor_bytes_not_path_guess(self):
        self.put()
        self.assertIsNone(self.store.get("task", key(b"changed exact task")))

    def test_19_invalid_path_rejected(self):
        for bad in ("../outside", "a" * 63, "A" * 64, "g" * 64):
            with self.assertRaises(ValueError):
                self.store.path("task", bad)

    def test_20_eviction_cannot_change_answer(self):
        # An independent cold oracle is a function of input only, never of cache state.
        def cold(x):
            return str(sum(i * i for i in range(x))).encode("ascii")
        for x in range(30):
            k = key(str(x).encode("ascii"))
            expected = cold(x)
            value = self.store.get("task", k)
            if value is None:
                value = cold(x)
                self.assertTrue(self.store.put("task", k, value))
            self.assertEqual(expected, value)
            self.store.path("task", k).unlink()
            self.assertEqual(expected, cold(x))


if __name__ == "__main__":
    unittest.main(verbosity=2)
