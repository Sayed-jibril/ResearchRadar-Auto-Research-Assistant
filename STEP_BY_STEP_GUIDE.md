# 🎯 ScholarLens - STEP BY STEP WORKING GUIDE

## ✅ GUARANTEED WORKING STEPS

### **Step 1: Open PowerShell Terminal**
1. Press `Windows + R`
2. Type `powershell` 
3. Press Enter

### **Step 2: Navigate to Backend**
Copy and paste this EXACT command:
```powershell
cd "C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\backend\ScholarLens.Api"
```

### **Step 3: Start Backend**
Copy and paste this EXACT command:
```powershell
dotnet run --urls "http://localhost:5000"
```

You should see:
```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### **Step 4: Test Backend (New Terminal)**
1. Open a NEW PowerShell terminal
2. Copy and paste:
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET
```

Expected result: `Healthy`

### **Step 5: Test Search API**
Copy and paste:
```powershell
$body = '{"query":"artificial intelligence","limit":5}'
Invoke-RestMethod -Uri "http://localhost:5000/api/search" -Method POST -Body $body -ContentType "application/json"
```

## 🚀 **Start All Services (Optional)**

### **Terminal 1 - Backend**
```powershell
cd "C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\backend\ScholarLens.Api"
dotnet run --urls "http://localhost:5000"
```

### **Terminal 2 - NLP Service** 
```powershell
cd "C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\nlp-service"
python -m uvicorn main:app --reload --host 0.0.0.0 --port 8000
```

### **Terminal 3 - Frontend**
```powershell
cd "C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\frontend"
npm run dev
```

## 📱 **URLs After Starting**

- **Backend API**: http://localhost:5000
- **API Documentation**: http://localhost:5000/swagger  
- **NLP Service**: http://localhost:8000
- **Frontend**: http://localhost:3000

## 🔧 **If Something Doesn't Work**

### Backend Issues:
```powershell
# Kill existing processes
Stop-Process -Name "dotnet" -Force -ErrorAction SilentlyContinue

# Navigate and restart
cd "C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\backend\ScholarLens.Api"
dotnet run --urls "http://localhost:5000"
```

### Check if Services are Running:
```powershell
# Check processes
Get-Process -Name "*dotnet*", "*python*", "*node*" -ErrorAction SilentlyContinue

# Check ports
netstat -ano | findstr ":5000"
netstat -ano | findstr ":8000"
netstat -ano | findstr ":3000"
```

## 🎊 **Success Indicators**

✅ **Backend Working**: Health endpoint returns "Healthy"  
✅ **Search Working**: Returns academic paper results  
✅ **NLP Working**: Health endpoint returns JSON  
✅ **Frontend Working**: Shows web interface

## 🎯 **Quick Test Commands**

```powershell
# Health check
Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET

# Search test
$search = @{
    query = "machine learning"
    limit = 3
} | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5000/api/search" -Method POST -Body $search -ContentType "application/json"

# NLP health
Invoke-RestMethod -Uri "http://localhost:8000/health" -Method GET
```

## 🏆 **What ScholarLens Does**

🔍 **Searches** multiple academic databases  
📄 **Extracts** text from research papers  
🤖 **Summarizes** using AI models  
📊 **Generates** professional PDF reports  

**You now have a complete academic research assistant!** 🎓📚
