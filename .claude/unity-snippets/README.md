C# method bodies run in the open Editor with `unity command script eval_file`.
They live outside Assets/ so Unity never compiles them. Keep each one idempotent and returning a short summary string.
`_catalog.json` is a per-session cache of `unity command --detail full --format json` (safe to delete).
