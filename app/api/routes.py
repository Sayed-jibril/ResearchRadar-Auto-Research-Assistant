"""
API routes for the NLP service
"""
import logging
from typing import Dict, Any

from fastapi import APIRouter, HTTPException, Depends, status
from fastapi.responses import JSONResponse

from app.models.schemas import (
    ExtractTextRequest, ExtractTextResponse,
    SummarizeRequest, SummarizeResponse,
    HealthResponse, ErrorResponse
)
from app.services.text_extractor import TextExtractor
from app.services.summarizer import TextSummarizer
from app.services.model_manager import ModelManager

logger = logging.getLogger(__name__)

# Create router
router = APIRouter()

# Global service instances (will be injected)
text_extractor = TextExtractor()
model_manager: ModelManager = None
summarizer: TextSummarizer = None


def get_model_manager() -> ModelManager:
    """Dependency to get model manager"""
    from main import model_manager as global_model_manager
    if not global_model_manager or not global_model_manager.is_initialized:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Model manager not initialized"
        )
    return global_model_manager


def get_summarizer(mm: ModelManager = Depends(get_model_manager)) -> TextSummarizer:
    """Dependency to get summarizer"""
    return TextSummarizer(mm)


@router.post(
    "/extract-text",
    response_model=ExtractTextResponse,
    responses={
        400: {"model": ErrorResponse, "description": "Invalid request"},
        422: {"model": ErrorResponse, "description": "URL processing failed"},
        503: {"model": ErrorResponse, "description": "Service unavailable"}
    },
    summary="Extract text from PDF",
    description="Download a PDF from the provided URL and extract its text content"
)
async def extract_text(request: ExtractTextRequest) -> ExtractTextResponse:
    """Extract text from a PDF URL"""
    try:
        logger.info(f"Extracting text from PDF: {request.pdf_url}")
        
        # Extract text
        text, char_count = await text_extractor.extract_from_url(str(request.pdf_url))
        
        logger.info(f"Successfully extracted {char_count} characters")
        
        return ExtractTextResponse(
            text=text,
            chars=char_count
        )
        
    except ValueError as e:
        logger.error(f"Validation error in text extraction: {e}")
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=str(e)
        )
    except Exception as e:
        logger.error(f"Unexpected error in text extraction: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Internal server error during text extraction"
        )


@router.post(
    "/summarize",
    response_model=SummarizeResponse,
    responses={
        400: {"model": ErrorResponse, "description": "Invalid request"},
        422: {"model": ErrorResponse, "description": "Summarization failed"},
        503: {"model": ErrorResponse, "description": "Service unavailable"}
    },
    summary="Summarize text",
    description="Generate a structured summary of the provided text using AI models"
)
async def summarize_text(
    request: SummarizeRequest,
    summarizer: TextSummarizer = Depends(get_summarizer)
) -> SummarizeResponse:
    """Summarize text with structured output"""
    try:
        logger.info(f"Summarizing text: {len(request.text)} chars, style={request.style}, lang={request.lang}")
        
        # Generate summary
        result = await summarizer.summarize(
            text=request.text,
            style=request.style,
            language=request.lang,
            max_tokens=request.max_tokens
        )
        
        logger.info("Successfully generated structured summary")
        
        return SummarizeResponse(
            tl_dr=result.tl_dr,
            key_points=result.key_points,
            methods=result.methods,
            results=result.results,
            limitations=result.limitations
        )
        
    except ValueError as e:
        logger.error(f"Validation error in summarization: {e}")
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=str(e)
        )
    except Exception as e:
        logger.error(f"Unexpected error in summarization: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Internal server error during summarization"
        )


@router.get(
    "/health",
    response_model=HealthResponse,
    summary="Service health check",
    description="Check the health status of the NLP service and its models"
)
async def health_check(
    mm: ModelManager = Depends(get_model_manager)
) -> HealthResponse:
    """Detailed health check for the NLP service"""
    try:
        models_loaded = await mm.health_check()
        extractor_healthy = await text_extractor.health_check()
        
        # Service is healthy if models are loaded and extractor works
        service_healthy = models_loaded and extractor_healthy
        
        return HealthResponse(
            status="healthy" if service_healthy else "degraded",
            models_loaded=models_loaded,
            available_languages=mm.available_languages
        )
        
    except Exception as e:
        logger.error(f"Health check failed: {e}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Service health check failed"
        )


@router.get(
    "/status",
    summary="Service status",
    description="Get basic service status information"
)
async def get_status() -> Dict[str, Any]:
    """Get service status and information"""
    try:
        return {
            "service": "nlp-service",
            "version": "1.0.0",
            "status": "running",
            "endpoints": [
                "/api/v1/extract-text",
                "/api/v1/summarize",
                "/api/v1/health",
                "/api/v1/status"
            ]
        }
    except Exception as e:
        logger.error(f"Status check failed: {e}")
        return {
            "service": "nlp-service",
            "version": "1.0.0",
            "status": "error",
            "error": str(e)
        }
