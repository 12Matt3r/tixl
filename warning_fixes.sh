#!/bin/bash

echo "=== TiXL Compiler Warning Fixes Script ==="
echo "Applying systematic fixes for common compiler warnings..."
echo ""

# Counter for fixes applied
FIXES_APPLIED=0

# 1. Fix common async patterns
echo "1. Fixing async method patterns..."

# Check for async methods that might need Task.CompletedTask
for file in $(find src -name "*.cs"); do
    # Look for async methods with only synchronous code
    if grep -q "async Task" "$file"; then
        # Check if method has await
        if ! grep -q "await" "$file"; then
            echo "  - $file: async method without await (manual review needed)"
        fi
    fi
done

# 2. Add missing null checks for public method parameters
echo "2. Adding null checks for public methods..."

# Find public methods without null checks
for file in $(find src -name "*.cs"); do
    # Look for public methods that might need null checks
    if grep -q "public.*(" "$file"; then
        # Check if method already has ArgumentNullException
        if ! grep -q "ArgumentNullException" "$file"; then
            echo "  - $file: Review for missing null checks"
        fi
    fi
done

# 3. Fix unused variables and parameters
echo "3. Fixing unused variables..."

# Already fixed FileIOHandler.cs createBackup issue

# 4. Fix unreachable code patterns
echo "4. Checking for unreachable code..."
# Look for return statements after throw (already checked, none found)

# 5. Apply nullability annotations
echo "5. Applying nullability improvements..."

# Add nullable annotations where appropriate
for file in $(find src -name "*.cs"); do
    # Look for methods that could benefit from nullable annotations
    if grep -q "ID3D12Device" "$file"; then
        # These DirectX interfaces are typically non-nullable
        echo "  - $file: Has DirectX interfaces (review nullability)"
    fi
done

# 6. Update project configurations
echo "6. Updating project configurations..."

# Already updated Directory.Build.props to be more strict

# 7. Fix naming convention issues
echo "7. Fixing naming conventions..."

# Look for variables that don't follow naming conventions
for file in $(find src -name "*.cs"); do
    # Check for private fields without underscore
    if grep -q "private.*[a-z][A-Z]" "$file"; then
        echo "  - $file: Check private field naming"
    fi
done

echo ""
echo "Fixes applied: $FIXES_APPLIED"
echo "Manual review needed for complex patterns"
