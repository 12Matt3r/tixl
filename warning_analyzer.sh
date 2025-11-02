#!/bin/bash

echo "=== TiXL Compiler Warning Analysis and Fixes ==="
echo "Analyzing C# source files for common warning patterns..."
echo ""

# Find all C# files
CS_FILES=$(find src -name "*.cs" -type f)
TOTAL_FILES=$(echo "$CS_FILES" | wc -l)
echo "Found $TOTAL_FILES C# files to analyze"

# Create output directory for analysis results
mkdir -p analysis_results

# 1. Check for unused variables (variable assignments that don't use the variable)
echo "1. Checking for unused variables..."
for file in $CS_FILES; do
    # Look for patterns like "var x = y; x = x;" or "_var = _var;"
    grep -n "_*[a-zA-Z][a-zA-Z0-9]*\s*=\s*_\*[a-zA-Z][a-zA-Z0-9]*\s*;" "$file" 2>/dev/null >> analysis_results/unused_variables.txt || true
    grep -n "[a-zA-Z][a-zA-Z0-9]*\s*=\s*[a-zA-Z][a-zA-Z0-9]*\s*;" "$file" 2>/dev/null >> analysis_results/unused_variables.txt || true
done

# 2. Check for async methods without await
echo "2. Checking for async methods without await..."
for file in $CS_FILES; do
    # Find async methods and check if they use await
    awk '/async\s+(Task\s+[A-Za-z_][\w]*|Task\s*<|Task\s*\(\)|ValueTask)/ {
        async_line = NR
        in_async = 1
        has_await = 0
        
        # Check next 50 lines for await
        for(i = NR; i <= NR+50 && i <= FNR; i++) {
            getline
            if(/await\s+/) {
                has_await = 1
                break
            }
            if(/^[[:space:]]*(public|private|protected|internal|\})/) {
                if(i > NR+5) break
            }
        }
        
        if(!has_await) {
            print FILENAME ":" async_line ": async method without await"
        }
    }' "$file" >> analysis_results/async_without_await.txt || true
done

# 3. Check for unreachable code (return after throw)
echo "3. Checking for unreachable code..."
for file in $CS_FILES; do
    awk '/throw\s+.*;/ { throw_line = NR } 
         /throw\s+.*;/ { next_line = NR+1; getline; if(/return/) print FILENAME ":" next_line ": possible unreachable code after throw"}' "$file" >> analysis_results/unreachable_code.txt || true
done

# 4. Check for missing null checks on parameters
echo "4. Checking for missing null checks..."
for file in $CS_FILES; do
    # Look for public method parameters that might need null checks
    awk '/public\s+\w+\s+\w+\s*\([^)]*\)\s*\{/ {
        line = NR
        brace_line = 0
        for(i = NR; i <= NR+10; i++) {
            getline
            if(/\{/) { brace_line = i; break }
        }
        if(brace_line > 0) {
            # Check first few lines after opening brace for null checks
            for(i = brace_line; i <= brace_line+5; i++) {
                getline
                if(/ArgumentNullException.*\(.+\)/) { next_file = 1; break }
            }
            if(next_file != 1) {
                print FILENAME ":" line ": possible missing null check"
            }
        }
    }' "$file" >> analysis_results/missing_null_checks.txt || true
done

# 5. Check for unused parameters with underscore prefix
echo "5. Checking for unused parameters..."
for file in $CS_FILES; do
    # Look for methods with parameters that start with underscore (convention for unused)
    grep -n "(_[a-zA-Z][a-zA-Z0-9]*)\.Ignore()" "$file" >> analysis_results/unused_parameters.txt || true
done

echo ""
echo "Analysis complete. Results saved to analysis_results/"
echo ""
echo "Summary of findings:"
echo "- Unused variables: $(wc -l < analysis_results/unused_variables.txt 2>/dev/null || echo 0)"
echo "- Async without await: $(wc -l < analysis_results/async_without_await.txt 2>/dev/null || echo 0)"
echo "- Unreachable code: $(wc -l < analysis_results/unreachable_code.txt 2>/dev/null || echo 0)"
echo "- Missing null checks: $(wc -l < analysis_results/missing_null_checks.txt 2>/dev/null || echo 0)"
echo "- Unused parameters: $(wc -l < analysis_results/unused_parameters.txt 2>/dev/null || echo 0)"
