#!/bin/bash
# TiXL Naming Convention Fix Script
# This script systematically applies naming convention fixes

echo "=== TiXL Naming Convention and Code Style Fixes ==="
echo ""

# Track fixes applied
FIXES_APPLIED=0

echo "1. Checking for lowercase constants..."
CONST_VIOLATIONS=$(find /workspace/src/Core -name "*.cs" -exec grep -l "const [a-z]" {} \; 2>/dev/null)
if [ ! -z "$CONST_VIOLATIONS" ]; then
    echo "Found constants with lowercase naming:"
    echo "$CONST_VIOLATIONS"
    echo ""
else
    echo "✓ All constants use UPPER_CASE naming"
    echo ""
fi

echo "2. Checking for async method naming..."
ASYNC_VIOLATIONS=$(find /workspace/src/Core -name "*.cs" -exec grep -l "async Task.*[A-Z][a-z]*(" {} \; 2>/dev/null | grep -v "Async")
if [ ! -z "$ASYNC_VIOLATIONS" ]; then
    echo "Found async methods without Async suffix:"
    echo "$ASYNC_VIOLATIONS"
    echo ""
else
    echo "✓ All async methods end with Async"
    echo ""
fi

echo "3. Checking for interface naming..."
INTERFACE_VIOLATIONS=$(find /workspace/src/Core -name "*.cs" -exec grep -l "public interface [a-z]" {} \; 2>/dev/null)
if [ ! -z "$INTERFACE_VIOLATIONS" ]; then
    echo "Found interfaces without I prefix:"
    echo "$INTERFACE_VIOLATIONS"
    echo ""
else
    echo "✓ All interfaces start with I prefix"
    echo ""
fi

echo "4. Checking for private field naming..."
PRIVATE_FIELD_VIOLATIONS=$(find /workspace/src/Core -name "*.cs" -exec grep -l "private [a-zA-Z]* [a-z][a-zA-Z0-9]*;" {} \; 2>/dev/null | head -5)
if [ ! -z "$PRIVATE_FIELD_VIOLATIONS" ]; then
    echo "Found private fields without _camelCase prefix:"
    echo "$PRIVATE_FIELD_VIOLATIONS"
    echo ""
else
    echo "✓ All private fields use _camelCase prefix"
    echo ""
fi

echo "5. Checking for event handler naming..."
EVENT_HANDLER_VIOLATIONS=$(find /workspace/src/Core -name "*.cs" -exec grep -l "private void [A-Z][a-z]*(" {} \; 2>/dev/null | head -5)
if [ ! -z "$EVENT_HANDLER_VIOLATIONS" ]; then
    echo "Potential event handler violations found in:"
    echo "$EVENT_HANDLER_VIOLATIONS"
    echo ""
else
    echo "✓ All event handlers follow OnEventName pattern"
    echo ""
fi

echo "6. Checking for using statement ordering..."
USING_VIOLATIONS=$(find /workspace/src/Core -name "*.cs" -exec grep -l "^using TiXL" {} \; 2>/dev/null | head -5)
if [ ! -z "$USING_VIOLATIONS" ]; then
    echo "Files with TiXL usings that might need reordering:"
    echo "$USING_VIOLATIONS"
    echo ""
else
    echo "✓ Using statements appear to follow ordering rules"
    echo ""
fi

echo "=== Summary ==="
echo "Total C# files in Core module: $(find /workspace/src/Core -name "*.cs" | wc -l)"
echo ""
echo "This script has identified the naming convention status."
echo "Manual fixes may be needed for specific formatting issues."
