#!/bin/bash
# Usage: _run.sh <snippet.cs>  -> prints only the snippet's result (or the error)
unity command script eval --project-path "E:\Project\game-jam" --caller plugin --skill unity-cli-loop --format json -- --file "$1" 2>/dev/null | python -c "import sys,json; d=json.load(sys.stdin); print(d.get('result') if d.get('success') else d)"
