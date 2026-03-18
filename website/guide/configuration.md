# Configuration

Shelf requires minimal configuration. Most setups work with just Docker volume mounts and zero environment variables.

## Options

Configuration is done via environment variables using the ASP.NET Core configuration pattern:

| Option | Env Variable | Default | Description |
|---|---|---|---|
| `DocsRoot` | `Shelf__DocsRoot` | `/data/docs` | Root directory where product documentation is stored |
| `VersionPattern` | `Shelf__VersionPattern` | `^v\d+$` | Regex pattern to identify version directories |

## Version Pattern

By default, Shelf recognizes directories matching `v` followed by a number as version directories: `v1`, `v2`, `v10`, etc.

Directories that don't match this pattern are ignored during version scanning. This means you can have non-version directories alongside version directories without issues:

```
/data/docs/configuration/
├── v4/          ← recognized as version
├── v5/          ← recognized as version
├── assets/      ← ignored (not a version)
└── .hidden/     ← ignored (not a version)
```

## Customizing the Version Pattern

If you need a different versioning scheme, override the pattern:

```yaml
environment:
  - Shelf__VersionPattern=^v\d+\.\d+$    # matches v1.0, v2.1, etc.
```

::: warning
Changing the version pattern affects how "latest" is determined. The default sorting extracts the number after `v` and sorts numerically. Custom patterns with non-numeric segments may not sort as expected.
:::
