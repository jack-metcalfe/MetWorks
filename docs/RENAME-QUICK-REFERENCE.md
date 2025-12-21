# Quick Reference: Repository Renaming

## TL;DR - Fast Track

### 1. Rename on GitHub (30 seconds)
1. Go to Settings → Repository name
2. Enter new name → Click "Rename"

### 2. Update Your Local Clone (30 seconds)
```bash
git remote set-url origin https://github.com/VikingsFan1024/NEW-NAME
```

### 3. Update These Files in the Repo
- **README.md** - Line 1 (title) and Line 8 (clone URL)
- **CONTRIBUTING.md** - References to solution name (if renaming solution too)

### 4. Check for Other References (1 minute)
```bash
bash scripts/check-repo-references.sh NEW-NAME VikingsFan1024
```

### 5. Notify Team
- Send email/message with new URL
- Update any documentation or wikis
- Update CI/CD external integrations (if any)

## Common Scenarios

### Scenario 1: Just Rename the GitHub Repository
**What changes:** Repository URL  
**What stays same:** Solution names, namespaces, project names  
**Files to update:** README.md (mainly the clone URL)

### Scenario 2: Rename Everything (Repository + Solution + Namespaces)
**What changes:** Everything  
**What stays same:** Git history  
**Files to update:** MANY - use the check script and see full guide

## Most Common Files to Update

| File | What to Change | Required? |
|------|----------------|-----------|
| README.md | Title & clone URL | ✅ Yes |
| CONTRIBUTING.md | Solution name references | Only if renaming solution |
| docs/*.md | Repository references | Optional but recommended |
| .github/workflows/*.yml | Usually auto-updated by GitHub | ✅ Verify |

## Important Reminders

✅ **DO:**
- Notify all collaborators
- Update local remotes: `git remote set-url origin <new-url>`
- Test that CI/CD still works
- Update external services/webhooks

❌ **DON'T:**
- Rename if you have published packages referencing this URL
- Rename during active development (coordinate with team)
- Forget to update documentation

## Need More Help?

📖 **Full Guide:** [docs/HOW-TO-RENAME-REPOSITORY.md](HOW-TO-RENAME-REPOSITORY.md)  
🔧 **Check Script:** `bash scripts/check-repo-references.sh`

## One-Liner Checklist

```bash
# After renaming on GitHub, run these:
git remote set-url origin https://github.com/OWNER/NEW-NAME
git pull
bash scripts/check-repo-references.sh NEW-NAME OWNER
# Update files shown by the script
git add .
git commit -m "Update repository references after rename"
git push
```

Done! 🎉
