# Simple ScholarLens Demo - No Database Required
# This script demonstrates the key features without requiring PostgreSQL

Write-Host "🚀 ScholarLens Simple Demo" -ForegroundColor Green
Write-Host "=========================="
Write-Host ""

Write-Host "📋 Available Demo Options:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1️⃣  Backend Only (requires PostgreSQL/Redis)" -ForegroundColor Cyan
Write-Host "   cd backend/ScholarLens.Api && dotnet run"
Write-Host ""
Write-Host "2️⃣  Full Stack with Docker (recommended)" -ForegroundColor Cyan  
Write-Host "   cd infra && docker compose up --build"
Write-Host ""
Write-Host "3️⃣  API Documentation" -ForegroundColor Cyan
Write-Host "   Visit: http://localhost:5000/swagger (when backend is running)"
Write-Host ""

Write-Host "🔧 Current Status Check:" -ForegroundColor Yellow
Write-Host ""

# Check if Docker is available
try {
    $dockerVersion = docker --version 2>$null
    if ($dockerVersion) {
        Write-Host "✅ Docker is available: $dockerVersion" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Docker not found" -ForegroundColor Red
}

# Check if dotnet is available
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion) {
        Write-Host "✅ .NET is available: Version $dotnetVersion" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ .NET not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "🎯 Recommended Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "For Full Experience:" -ForegroundColor Cyan
Write-Host "1. cd infra"
Write-Host "2. docker compose up --build"
Write-Host "3. Wait 2-3 minutes for all services to start"
Write-Host "4. Visit:"
Write-Host "   - Frontend: http://localhost:3000"
Write-Host "   - Backend API: http://localhost:8080"
Write-Host "   - Swagger UI: http://localhost:8080/swagger"
Write-Host ""

Write-Host "For Quick API Test:" -ForegroundColor Cyan
Write-Host "1. Start PostgreSQL and Redis (or use Docker)"
Write-Host "2. cd backend/ScholarLens.Api"
Write-Host "3. dotnet run"
Write-Host "4. Visit: http://localhost:5000/swagger"
Write-Host ""

Write-Host "📚 API Endpoints Available:" -ForegroundColor Yellow
Write-Host "   POST /api/search     - Search academic papers"
Write-Host "   POST /api/ingest     - Extract PDF text"
Write-Host "   POST /api/summarize  - Generate AI summaries"
Write-Host "   POST /api/report     - Create PDF reports"
Write-Host "   GET  /health         - System health"
Write-Host ""

Write-Host "📁 Test Files Available:" -ForegroundColor Yellow
Write-Host "   docs/api.http        - REST client tests"
Write-Host "   demo-full-pipeline.ps1 - Complete workflow test"
Write-Host ""

$choice = Read-Host "Would you like to start Docker Compose now? (y/n)"
if ($choice -eq 'y' -or $choice -eq 'Y') {
    Write-Host "🚀 Starting Docker Compose..." -ForegroundColor Green
    Set-Location "infra"
    docker compose up --build
} else {
    Write-Host "👍 You can start it manually when ready!" -ForegroundColor Green
    Write-Host "💡 Tip: Use 'docker compose up --build' in the infra/ directory" -ForegroundColor Cyan
}
