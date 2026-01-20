# Fix .expo Directory Git Tracking

If `.expo/` is already tracked by Git, you need to remove it from tracking:

```bash
# Remove .expo from git tracking (but keep the directory)
git rm -r --cached .expo

# Commit the change
git commit -m "Remove .expo from git tracking"
```

The `.gitignore` file already has `.expo/` listed, so it won't be tracked in the future.
