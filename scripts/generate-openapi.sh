#!/usr/bin/env bash
# Regenerates openapi.json and the frontend generated type files.
# Run this after any C# contract or enum change.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Building API..."
dotnet build "$REPO_ROOT/src/Gatherstead.Api/Gatherstead.Api.csproj" --configuration Release

echo "Restoring dotnet tools..."
dotnet tool restore --tool-manifest "$REPO_ROOT/.config/dotnet-tools.json"

echo "Generating openapi.json..."
dotnet swagger tofile \
    --output "$REPO_ROOT/src/Gatherstead.Api/openapi.json" \
    "$REPO_ROOT/src/Gatherstead.Api/bin/Release/net10.0/Gatherstead.Api.dll" v1

echo "Generating TypeScript API types..."
cd "$REPO_ROOT/src/Gatherstead.Web"
pnpm generate:types

echo "Generating flag constants..."
cd "$REPO_ROOT"
dotnet run --project tools/FlagsCodegen/FlagsCodegen.csproj -- \
    "$REPO_ROOT/src/Gatherstead.Web/app/repositories/generated/flags.gen.ts"

echo "Done. Commit the updated files in:"
echo "  src/Gatherstead.Api/openapi.json"
echo "  src/Gatherstead.Web/app/repositories/generated/"
