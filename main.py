"""
ScholarLens NLP Service - FastAPI application for text extraction and summarization
"""
import os
import logging
from contextlib import asynccontextmanager
from typing import Dict, Any

from fastapi import FastAPI, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware
from dotenv import load_dotenv

from app.api.routes import router
from app.core.config import settings
from app.services.model_manager import ModelManager

# Load environment variables
load_dotenv()

# Configure logging
logging.basicConfig(
    level=getattr(logging, os.getenv("LOG_LEVEL", "INFO")),
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)

logger = logging.getLogger(__name__)

# Global model manager instance
model_manager: ModelManager = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager - handles startup and shutdown"""
    global model_manager
    
    # Startup
    logger.info("Starting ScholarLens NLP Service...")
    try:
        model_manager = ModelManager()
        await model_manager.initialize()
        logger.info("Model manager initialized successfully")
    except Exception as e:
        logger.error(f"Failed to initialize model manager: {e}")
        raise
    
    yield
    
    # Shutdown
    logger.info("Shutting down ScholarLens NLP Service...")
    if model_manager:
        await model_manager.cleanup()
    logger.info("Shutdown complete")


# Create FastAPI application
app = FastAPI(
    title="ScholarLens NLP Service",
    description="Text extraction and summarization service for academic papers",
    version="1.0.0",
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc"
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, specify exact origins
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Health check endpoint
@app.get("/health", tags=["Health"])
async def health_check() -> Dict[str, Any]:
    """Health check endpoint for Docker and load balancers"""
    try:
        # Check if model manager is available and healthy
        if model_manager and await model_manager.health_check():
            return {
                "status": "healthy",
                "service": "nlp-service",
                "version": "1.0.0",
                "models_loaded": True
            }
        else:
            return {
                "status": "unhealthy",
                "service": "nlp-service",
                "version": "1.0.0",
                "models_loaded": False
            }
    except Exception as e:
        logger.error(f"Health check failed: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Service unhealthy"
        )

# Include API routes
app.include_router(router, prefix="/api/v1")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG,
        log_level=settings.LOG_LEVEL.lower()
    )
