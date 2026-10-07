# 🚀 ScholarLens - Complete Run Guide

This document provides step-by-step instructions to run all components of the ScholarLens AI Research Assistant.

## 📋 Prerequisites

Before running any component, ensure you have:
- ✅ **Node.js** (v18 or higher)
- ✅ **Python** (v3.8 or higher)
- ✅ **.NET 8 SDK**
- ✅ **Git** (for cloning/updating)
- ✅ **XAMPP** (for local development)

---

## 🎯 Quick Start (All Services)

### 1. **Frontend (Next.js)**
```bash
# Navigate to project root
cd C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant

# Install dependencies (if not already done)
npm install

# Start development server
npm run dev
```
**🌐 Access:** http://localhost:3000

### 2. **Backend (.NET API)**
```bash
# Navigate to .NET backend
cd C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\backend\ScholarLens.Api

# Restore packages
dotnet restore

# Run the API
dotnet run
```
**🔗 API Endpoint:** http://localhost:5182

### 3. **NLP Service (Python)**
```bash
# Navigate to NLP service
cd C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\nlp-service

# Install Python dependencies
pip install -r requirements.txt

# Start the NLP service
python main.py
```
**🔗 NLP Endpoint:** http://localhost:8000

---

## 📁 Detailed Component Setup

### 🔧 **Frontend Setup (Next.js)**

**Location:** `C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant`

```bash
# 1. Navigate to project root
cd C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant

# 2. Install dependencies
npm install

# 3. Start development server
npm run dev

# 4. Build for production (optional)
npm run build
npm start
```

**Configuration Files:**
- `package.json` - Dependencies and scripts
- `next.config.js` - Next.js configuration
- `tailwind.config.js` - Tailwind CSS configuration
- `tsconfig.json` - TypeScript configuration

**Key Features:**
- ✅ Modern React with Next.js 14
- ✅ TypeScript support
- ✅ Tailwind CSS styling
- ✅ API integration with .NET backend
- ✅ Search functionality
- ✅ Responsive design

---

### ⚙️ **Backend Setup (.NET API)**

**Location:** `C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\backend\ScholarLens.Api`

```bash
# 1. Navigate to .NET backend
cd C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\backend\ScholarLens.Api

# 2. Restore NuGet packages
dotnet restore

# 3. Run in development mode
dotnet run

# 4. Run with specific environment (optional)
dotnet run --environment Development

# 5. Build for production (optional)
dotnet build --configuration Release
dotnet run --configuration Release
```

**Configuration Files:**
- `appsettings.json` - Main configuration
- `appsettings.Development.json` - Development settings
- `Program.cs` - Application entry point
- `ScholarLens.Api.csproj` - Project file

**API Endpoints:**
- `GET /api/search` - Search academic papers
- `POST /api/ingest` - Ingest papers
- `POST /api/summarize` - Generate summaries
- `POST /api/report` - Generate reports

**Database:**
- Entity Framework Core with SQL Server
- Connection string in `appsettings.json`

---

### 🤖 **NLP Service Setup (Python)**

**Location:** `C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\nlp-service`

```bash
# 1. Navigate to NLP service
cd C:\xampp\htdocs\ScholarLens---Auto-Research-Assistant\scholarlens\nlp-service

# 2. Create virtual environment (recommended)
python -m venv venv

# 3. Activate virtual environment
# On Windows:
venv\Scripts\activate

# 4. Install dependencies
pip install -r requirements.txt

# 5. Start the service
python main.py

# 6. Alternative: Run with uvicorn (for production)
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

**Configuration Files:**
- `requirements.txt` - Python dependencies
- `main.py` - FastAPI application entry point
- `app/` - Application modules

**Key Features:**
- ✅ FastAPI framework
- ✅ Natural language processing
- ✅ Text summarization
- ✅ Document analysis
- ✅ RESTful API endpoints

---

## 🔄 **Service Dependencies**

### **Startup Order:**
1. **Database** (if using external database)
2. **NLP Service** (Python - port 8000)
3. **Backend API** (.NET - port 5182)
4. **Frontend** (Next.js - port 3000)

### **Service Communication:**
```
Frontend (3000) → Backend API (5182) → NLP Service (8000)
```

---

## 🛠️ **Troubleshooting**

### **Common Issues:**

#### **Frontend Issues:**
```bash
# Clear Next.js cache
rm -rf .next
npm run dev

# Reinstall dependencies
rm -rf node_modules package-lock.json
npm install
```

#### **Backend Issues:**
```bash
# Clear .NET cache
dotnet clean
dotnet restore

# Check database connection
dotnet ef database update

# If build fails due to locked files (process already running):
# 1. Stop the running process
taskkill /f /im ScholarLens.Api.exe
# 2. Or find and kill the process
tasklist | findstr ScholarLens
taskkill /f /pid [PID_NUMBER]

# Alternative: Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

#### **NLP Service Issues:**
```bash
# Reinstall Python dependencies
pip uninstall -r requirements.txt -y
pip install -r requirements.txt

# Check Python version
python --version
```

### **Port Conflicts:**
- **Frontend:** Change port in `package.json` scripts
- **Backend:** Change port in `appsettings.json`
- **NLP Service:** Change port in `main.py`

### **File Locking Issues (.NET):**
If you get "file is locked by another process" error:
```bash
# Find the running process
tasklist | findstr ScholarLens

# Kill the process by name
taskkill /f /im ScholarLens.Api.exe

# Or kill by PID (replace [PID] with actual number)
taskkill /f /pid [PID]

# Then rebuild
dotnet clean
dotnet restore
dotnet run
```

---

## 📊 **Monitoring & Logs**

### **Frontend Logs:**
- Check browser console (F12)
- Terminal output from `npm run dev`

### **Backend Logs:**
- Check terminal output from `dotnet run`
- Application logs in `logs/` directory

### **NLP Service Logs:**
- Check terminal output from `python main.py`
- FastAPI logs in terminal

---

## 🚀 **Production Deployment**

### **Frontend:**
```bash
npm run build
npm start
```

### **Backend:**
```bash
dotnet publish -c Release
dotnet run --configuration Release
```

### **NLP Service:**
```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

---

## 📞 **Support**

If you encounter issues:
1. Check the troubleshooting section above
2. Verify all prerequisites are installed
3. Ensure all services are running on correct ports
4. Check console logs for error messages

**Default Ports:**
- Frontend: `http://localhost:3000`
- Backend API: `http://localhost:5182`
- NLP Service: `http://localhost:8000`

---

**🎉 Happy Researching with ScholarLens!**
