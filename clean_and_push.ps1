# Comprehensive cleanup and push script
Write-Host "Starting comprehensive cleanup..."

# Remove all large files and directories from git cache
Write-Host "Removing node_modules and other large files from git cache..."
git rm -r --cached node_modules 2>$null
git rm -r --cached .next 2>$null
git rm -r --cached scholarlens/backend/ScholarLens.Api/bin 2>$null
git rm -r --cached scholarlens/backend/ScholarLens.Api/obj 2>$null
git rm -r --cached scholarlens/frontend/node_modules 2>$null
git rm -r --cached scholarlens/frontend/.next 2>$null

# Add .gitignore file
Write-Host "Adding .gitignore..."
git add .gitignore

# Add all other important files
Write-Host "Adding important project files..."
git add *.md
git add *.json
git add *.txt
git add *.ps1
git add *.py
git add *.cs
git add *.tsx
git add *.ts
git add *.js
git add *.css
git add *.yml
git add *.yaml
git add Dockerfile
git add requirements.txt
git add package.json
git add tsconfig.json
git add next.config.js
git add tailwind.config.js

# Add directories (excluding node_modules and build files)
Write-Host "Adding project directories..."
git add app/
git add components/
git add lib/
git add pages/
git add src/
git add Controllers/
git add DTOs/
git add Models/
git add Services/
git add Data/
git add scholarlens/backend/ScholarLens.Api/Controllers/
git add scholarlens/backend/ScholarLens.Api/DTOs/
git add scholarlens/backend/ScholarLens.Api/Models/
git add scholarlens/backend/ScholarLens.Api/Services/
git add scholarlens/backend/ScholarLens.Api/Data/
git add scholarlens/backend/ScholarLens.Api/Program.cs
git add scholarlens/backend/ScholarLens.Api/ScholarLens.Api.csproj
git add scholarlens/frontend/src/
git add scholarlens/frontend/components/
git add scholarlens/frontend/lib/
git add scholarlens/frontend/package.json
git add scholarlens/frontend/tsconfig.json
git add scholarlens/frontend/next.config.js
git add scholarlens/frontend/tailwind.config.js
git add scholarlens/nlp-service/
git add scholarlens/db/

# Commit the changes
Write-Host "Committing changes..."
git commit -m "Clean up repository: remove large files and add proper .gitignore"

# Push to GitHub
Write-Host "Pushing to GitHub..."
git push origin main

Write-Host "Done!"
