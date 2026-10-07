# Final cleanup script to remove large files from git history
Write-Host "Starting final cleanup to remove large files from git history..."

# Remove the problematic large file specifically
Write-Host "Removing the specific large file from git history..."
git filter-branch --force --index-filter "git rm --cached --ignore-unmatch node_modules/@next/swc-win32-x64-msvc/next-swc.win32-x64-msvc.node" --prune-empty --tag-name-filter cat -- --all

# Clean up the git repository
Write-Host "Cleaning up git repository..."
git for-each-ref --format="delete %(refname)" refs/original | git update-ref --stdin
git reflog expire --expire=now --all
git gc --prune=now --aggressive

# Force push to GitHub
Write-Host "Force pushing to GitHub..."
git push origin main --force

Write-Host "Done! Large files should now be removed from git history."
