# Case Sensitivity Verification for Unix/DigitalOcean

## ✅ Verification Complete

All references have been verified for case-correctness. Here's what was checked:

### 1. Folder Names ✅
All folders use correct case: `TraditionalEats` (capital T and E)
- ✅ `src/services/TraditionalEats.*`
- ✅ `src/bff/TraditionalEats.*`
- ✅ `src/apps/TraditionalEats.*`
- ✅ `src/shared/TraditionalEats.*`
- ✅ `src/gateway/TraditionalEats.*`

### 2. Project Files (.csproj) ✅
All .csproj files match their folder names exactly:
- ✅ `TraditionalEats.IdentityService.csproj`
- ✅ `TraditionalEats.Contracts.csproj`
- ✅ `TraditionalEats.BuildingBlocks.csproj`
- ✅ etc.

### 3. ProjectReference Paths ✅
All ProjectReference paths use correct case:
```xml
<ProjectReference Include="..\..\shared\TraditionalEats.Contracts\TraditionalEats.Contracts.csproj" />
<ProjectReference Include="..\..\shared\TraditionalEats.BuildingBlocks\TraditionalEats.BuildingBlocks.csproj" />
```

**Note:** The backslashes (`\`) in paths are fine - .NET handles both `/` and `\` correctly on all platforms.

### 4. Namespaces ✅
All namespaces use correct case:
- ✅ `namespace TraditionalEats.IdentityService.*`
- ✅ `namespace TraditionalEats.BuildingBlocks.*`
- ✅ `namespace TraditionalEats.Contracts.*`

### 5. Using Statements ✅
All using statements use correct case:
- ✅ `using TraditionalEats.BuildingBlocks.*`
- ✅ `using TraditionalEats.IdentityService.*`

### 6. Package Names ✅
- ✅ `package.json`: `"name": "traditionaleats-mobile"` (lowercase is fine for npm packages)
- ✅ `app.json`: `"name": "TraditionalEats"` (correct case)

### 7. Solution File ✅
- ✅ `TraditionalEats.sln` (correct case)
- ✅ Paths in solution file use correct case

## Unix/DigitalOcean Compatibility

✅ **All paths and references are case-correct and will work on Unix/DigitalOcean**

The only lowercase references are:
- npm package name (`traditionaleats-mobile`) - this is correct for npm
- Docker container names (`tradition-eats-*`) - these are fine as identifiers

## Verification Script

Run this to verify everything:
```bash
./verify-case-sensitivity.sh
```

## Important Notes

1. **.NET handles path separators**: Both `/` and `\` work on all platforms
2. **Case matters on Unix**: All folder/file names must match exactly
3. **Namespaces are case-sensitive**: All `TraditionalEats` references must match exactly

## If You See Build Errors on DigitalOcean

If you get "file not found" errors on DigitalOcean:
1. Check that folder names match exactly (case-sensitive)
2. Verify .csproj file names match folder names
3. Ensure ProjectReference paths are correct
4. Run `dotnet restore` to refresh references
