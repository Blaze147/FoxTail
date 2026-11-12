#!/usr/bin/env python3
"""Continuously append sample logs. Run with an optional output path; stop with Ctrl+C."""
import datetime
import itertools
import pathlib
import sys
import time
path = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/demo.log')
path.parent.mkdir(parents=True, exist_ok=True)
print(path.resolve(), flush=True)
try:
    for index in itertools.count(1):
        level, message = [('INFO', 'Processing incoming records'), ('SUCCESS', 'Batch completed'), ('WARN', 'Queue approaching capacity'), ('ERROR', 'Connection timeout; retrying')][index % 4]
        with path.open('a', encoding='utf-8') as file:
            file.write(f'{datetime.datetime.now():%Y-%m-%d %H:%M:%S} {level:<7} [{index:06}] {message}\n')
        time.sleep(0.8)
except KeyboardInterrupt:
    pass
