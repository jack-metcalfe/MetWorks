# How to Rename This Repository

This guide explains how to rename the GitHub repository and what files need to be updated afterward.

## Part 1: Renaming on GitHub (Repository Settings)

### Steps to Rename the Repository:

1. **Navigate to Repository Settings**
   - Go to `https://github.com/VikingsFan1024/MetWorks`
   - Click on the **Settings** tab (requires admin/owner permissions)

2. **Rename the Repository**
   - In the Settings page, scroll down to the **Repository name** section
   - Enter your new repository name (e.g., `NewRepoName`)
   - Click the **Rename** button
   - GitHub will automatically redirect all requests from the old name to the new name

3. **Important Notes**
   - GitHub automatically sets up redirects from the old repository URL to the new one
   - Existing clones will continue to work temporarily, but should be updated
   - All issues, pull requests, wiki pages, and stars are preserved
   - GitHub Pages sites may need reconfiguration

### After Renaming on GitHub:

GitHub will show you a message with instructions. The key things to know:

- **Old URL**: `https://github.com/VikingsFan1024/MetWorks`
- **New URL**: `https://github.com/VikingsFan1024/NewRepoName` (example)

## Part 2: Update Local Git Remotes

All contributors need to update their local repository remotes:

```bash
# Check current remote
git remote -v

# Update the remote URL to the new repository name
git remote set-url origin https://github.com/VikingsFan1024/NewRepoName

# Verify the change
git remote -v
```

## Part 3: Update Files in the Repository

After renaming on GitHub, you'll need to update references to the repository name in the following files:

### Files That MUST Be Updated:

#### 1. **README.md**
- Line 1: Update the title `# MetWorks` to your new name
- Line 8: Update the clone command URL
  ```markdown
  git clone https://github.com/VikingsFan1024/NewRepoName.git
  ```

#### 2. **CONTRIBUTING.md** (if applicable)
- Line 23, 36: Update references to `MetWorks.sln` if you're also renaming the solution file

### Files That May Need Updates (depending on new name):

#### 3. **Package and Solution Files**
If you're changing the project namespace or solution name (not just the repository):
- `metworks-ddi-gen.sln` - Consider renaming the solution file
- Any `.csproj` files with namespace references
- `Directory.Build.props` - Check for hardcoded namespace references

#### 4. **Documentation Files**
Search for references in:
- `docs/Code Generation/README.md`
- `docs/decisions/ArchitectureAndDesignDecisions.md`
- `docs/decisions/adr-0003.md`
- `audit-report.md`

#### 5. **Script Files**
- `scripts/generate-audit.sh` - May contain repository-specific paths

#### 6. **Test Files**
- `tests/DdiCodeGen/Generator/Helpers/HarnessProjectHelper.cs`
- `tests/DdiCodeGen/Generator/fixtures/GeneratedHarness.csproj.tplt`

### Search for All References:

To find all references to the current repository name, run:

```bash
# Search for the repository name
grep -r "MetWorks" --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj .

# Search for the full GitHub URL
grep -r "VikingsFan1024/MetWorks" --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj .
```

## Part 4: Update CI/CD and External Services

After renaming, check and update:

### GitHub Actions
- ✅ Workflows in `.github/workflows/` should work automatically
- ✅ GitHub automatically updates internal references

### External Services (if applicable)
- [ ] Update any external CI/CD services (Travis CI, CircleCI, etc.)
- [ ] Update badges in README.md (build status, coverage, etc.)
- [ ] Update package registry configurations (NuGet, npm, etc.)
- [ ] Update documentation sites or wikis
- [ ] Update any webhooks
- [ ] Notify team members and update shared documentation

## Part 5: Verification Checklist

After completing the rename:

- [ ] Can you clone the repository using the new URL?
- [ ] Do all relative links in documentation still work?
- [ ] Do GitHub Actions workflows still run?
- [ ] Can you push to the repository using the new URL?
- [ ] Have you updated all local clones?
- [ ] Have you updated any bookmarks or references in external tools?
- [ ] Have you notified all collaborators?

## Important Warnings

⚠️ **Do NOT rename if:**
- You have packages published that reference this repository URL
- You have many external services/tools integrated with the current name
- You have many active pull requests (complete or close them first)

⚠️ **Consider:**
- Renaming causes temporary disruption for collaborators
- Links in old emails, documentation, or external sites may break
- Search engines may take time to update

## Alternatives to Renaming

If you just want to change how the project appears:
- Update the **Description** in repository settings (doesn't affect URLs)
- Update the **Topics** (tags) to improve discoverability
- Update the **README.md** title without changing the repository name

## Summary

1. **Rename on GitHub** via Settings → Repository name
2. **Update local remotes** on all clones: `git remote set-url origin <new-url>`
3. **Update file references** (mainly README.md and documentation)
4. **Verify and test** that everything still works
5. **Notify collaborators** about the change

The repository rename itself takes just a few seconds on GitHub. The real work is updating all the references and notifying everyone!
