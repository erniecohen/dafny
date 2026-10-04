#!/usr/bin/env python3
"""Record actual published compiler bytes without invoking the compiler."""
import argparse
import json
from pathlib import Path
from run import compiler_component_hashes, digest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("executable", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--product-revision", required=True)
    parser.add_argument("--archive", type=Path, required=True)
    args = parser.parse_args()
    receipt = {"schema_version": 1, "source_sha": args.source_sha,
               "last_product_revision": args.product_revision,
               "archive_sha256": digest(args.archive),
               "components_sha256": compiler_component_hashes(args.executable),
               "boundary": "actual published DLLs, deps/runtimeconfig JSON and native apphost; no execution"}
    args.output.write_text(json.dumps(receipt, indent=2) + "\n")


if __name__ == "__main__":
    main()
