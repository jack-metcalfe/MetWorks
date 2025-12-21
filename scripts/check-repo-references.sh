#!/bin/bash
# check-repo-references.sh
# This script helps identify all references to the repository name
# Run this before and after renaming to ensure all references are updated

set -e

REPO_NAME="${1:-MetWorks}"
OWNER="${2:-VikingsFan1024}"

echo "======================================"
echo "Repository Reference Checker"
echo "======================================"
echo "Searching for references to: $OWNER/$REPO_NAME"
echo ""

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to count and display matches
search_pattern() {
    local pattern=$1
    local description=$2
    
    echo -e "${YELLOW}Searching for: ${description}${NC}"
    
    # Count matches (excluding .git, bin, obj, node_modules)
    local count=$(grep -r "$pattern" \
        --exclude-dir=.git \
        --exclude-dir=bin \
        --exclude-dir=obj \
        --exclude-dir=node_modules \
        --exclude-dir=.vs \
        --exclude="*.dll" \
        --exclude="*.exe" \
        --exclude="package-lock.json" \
        . 2>/dev/null | wc -l)
    
    if [ "$count" -gt 0 ]; then
        echo -e "${RED}Found $count references:${NC}"
        grep -rn "$pattern" \
            --exclude-dir=.git \
            --exclude-dir=bin \
            --exclude-dir=obj \
            --exclude-dir=node_modules \
            --exclude-dir=.vs \
            --exclude="*.dll" \
            --exclude="*.exe" \
            --exclude="package-lock.json" \
            --exclude="check-repo-references.sh" \
            . 2>/dev/null | head -20
        
        if [ "$count" -gt 20 ]; then
            echo -e "${YELLOW}... and $(($count - 20)) more${NC}"
        fi
    else
        echo -e "${GREEN}No references found${NC}"
    fi
    echo ""
}

# Search for different patterns
echo "======================================"
echo "1. GitHub URL References"
echo "======================================"
search_pattern "github.com/$OWNER/$REPO_NAME" "Full GitHub URLs"

echo "======================================"
echo "2. Repository Name in Code"
echo "======================================"
search_pattern "$REPO_NAME" "Repository name (case-sensitive)"

echo "======================================"
echo "3. Owner/Repo Format"
echo "======================================"
search_pattern "$OWNER/$REPO_NAME" "Owner/Repo format"

echo "======================================"
echo "4. Clone Commands"
echo "======================================"
search_pattern "git clone.*$REPO_NAME" "Git clone commands"

echo "======================================"
echo "5. URLs in Markdown Files"
echo "======================================"
find . -name "*.md" \
    -not -path "./.git/*" \
    -not -path "./bin/*" \
    -not -path "./obj/*" \
    -exec grep -l "$REPO_NAME" {} \; 2>/dev/null | while read file; do
    echo -e "${YELLOW}$file${NC}"
    grep -n "$REPO_NAME" "$file" | head -5
done

echo ""
echo "======================================"
echo "Summary"
echo "======================================"
echo "Review the references above and update them after renaming the repository."
echo ""
echo "To use this script with a different name:"
echo "  bash scripts/check-repo-references.sh <new-repo-name> <owner>"
echo ""
echo "Example:"
echo "  bash scripts/check-repo-references.sh NewRepoName VikingsFan1024"
echo "======================================"
