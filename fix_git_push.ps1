# Fix git push issue by removing node_modules from tracking
Write-Host "Removing node_modules from git tracking..."

# Remove node_modules from git cache
git rm -r --cached node_modules

# Add .gitignore file
git add .gitignore

# Commit the changes
git commit -m "Remove node_modules from tracking and add .gitignore"

# Push to GitHub
Write-Host "Pushing to GitHub..."
git push origin main

Write-Host "Done!"
