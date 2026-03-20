# Contributing

Thanks for your interest in contributing to Shelf!

## Getting started
- Fork the repository and create a feature branch from `develop`.
- Ensure you have the .NET SDK specified in `global.json`.
- Run the test suite locally before opening a PR.

## Development setup

```bash
# Build and test
dotnet build ./src -c Release
dotnet test ./src -c Release

# Run locally
dotnet run --project ./src/Cocoar.Shelf -- \
  --Shelf:DocsRoot=./local-docs \
  --Shelf:ConfigRoot=./local-config

# VitePress docs
cd website && npm install && npm run dev
```

## Coding guidelines
- Prefer small, focused PRs.
- Add or update tests for all behavior changes.
- Ensure `dotnet build` passes with 0 errors and 0 warnings.

## Commit/PR
- Reference related issues in the PR description.
- Describe user-facing changes and migration notes if any.

## License
By contributing, you agree that your contributions will be licensed under the Apache-2.0 License.
