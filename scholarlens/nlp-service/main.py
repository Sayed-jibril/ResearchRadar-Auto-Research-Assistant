"""
ScholarLens NLP Service
FastAPI application for PDF text extraction and summarization
"""
from fastapi import FastAPI
from app.api.routes import router
import uvicorn

# Create FastAPI application
app = FastAPI(
    title="ScholarLens NLP Service",
    description="PDF text extraction and AI summarization service",
    version="1.0.0"
)

# Include API routes
app.include_router(router, prefix="/api")

# Health check endpoint
@app.get("/health")
async def health_check():
    return {"status": "healthy", "service": "nlp"}

@app.get("/")
async def root():
    return {"message": "ScholarLens NLP Service", "docs": "/docs"}

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
