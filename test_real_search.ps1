# Test script to verify real paper search results
Write-Host "Testing ScholarLens Real Paper Search..." -ForegroundColor Green

$headers = @{
    'Content-Type' = 'application/json'
}

$body = @{
    query = "machine learning"
    limit = 3
    yearFrom = 2023
    yearTo = 2024
} | ConvertTo-Json

Write-Host "Sending request to backend..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/search" -Method POST -Body $body -Headers $headers
    
    Write-Host "Success! Got response with $($response.results.Count) papers" -ForegroundColor Green
    
    foreach ($paper in $response.results) {
        Write-Host "---" -ForegroundColor Blue
        Write-Host "Title: $($paper.title)" -ForegroundColor White
        Write-Host "Source: $($paper.source)" -ForegroundColor Cyan
        Write-Host "Year: $($paper.year)" -ForegroundColor Gray
        if ($paper.source -ne "Mock") {
            Write-Host "✅ REAL PAPER FOUND!" -ForegroundColor Green
        } else {
            Write-Host "❌ Still showing mock data" -ForegroundColor Red
        }
    }
    
    Write-Host "---" -ForegroundColor Blue
    Write-Host "Metadata:" -ForegroundColor Yellow
    Write-Host "Total Results: $($response.metadata.totalResults)" -ForegroundColor White
    Write-Host "Processing Time: $($response.metadata.processingTime)" -ForegroundColor White
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure the backend is running on http://localhost:5000" -ForegroundColor Yellow
}
