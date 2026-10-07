"""
Pydantic models for request/response schemas
"""
from typing import List, Optional, Dict, Any
from pydantic import BaseModel, Field, HttpUrl, validator


class ExtractTextRequest(BaseModel):
    """Request model for PDF text extraction"""
    pdf_url: HttpUrl = Field(..., description="URL to the PDF file to extract text from")
    
    class Config:
        schema_extra = {
            "example": {
                "pdf_url": "https://arxiv.org/pdf/2301.00001.pdf"
            }
        }


class ExtractTextResponse(BaseModel):
    """Response model for PDF text extraction"""
    text: str = Field(..., description="Extracted text from the PDF")
    chars: int = Field(..., description="Number of characters in the extracted text")
    
    class Config:
        schema_extra = {
            "example": {
                "text": "Abstract\n\nThis paper presents...",
                "chars": 45672
            }
        }


class SummarizeRequest(BaseModel):
    """Request model for text summarization"""
    text: str = Field(..., description="Text to summarize", min_length=50)
    style: str = Field("technical", description="Summary style", regex="^(technical|executive)$")
    lang: str = Field("en", description="Language of the text", regex="^(en|zh)$")
    max_tokens: int = Field(1200, description="Maximum tokens for the summary", ge=100, le=2000)
    
    @validator('text')
    def validate_text_length(cls, v):
        if len(v) > 500000:  # 500K characters max
            raise ValueError('Text too long (max 500K characters)')
        return v
    
    class Config:
        schema_extra = {
            "example": {
                "text": "This paper investigates the application of digital twins...",
                "style": "technical",
                "lang": "en",
                "max_tokens": 1200
            }
        }


class SummarizeResponse(BaseModel):
    """Response model for text summarization"""
    tl_dr: str = Field(..., description="Brief summary (≤60 words)")
    key_points: List[str] = Field(..., description="List of key points")
    methods: str = Field(..., description="Methodology description")
    results: str = Field(..., description="Results and findings")
    limitations: List[str] = Field(..., description="List of limitations")
    
    class Config:
        schema_extra = {
            "example": {
                "tl_dr": "This study proposes a digital twin framework for plastic injection molding to predict defects using real-time sensor data and machine learning.",
                "key_points": [
                    "Digital twin framework for injection molding",
                    "Real-time defect prediction",
                    "Machine learning integration",
                    "Sensor data fusion"
                ],
                "methods": "The authors developed a digital twin using IoT sensors and CNN models for defect classification.",
                "results": "Achieved 94.2% accuracy in defect detection with 15% reduction in waste material.",
                "limitations": [
                    "Limited to specific polymer types",
                    "Requires extensive calibration",
                    "High computational overhead"
                ]
            }
        }


class HealthResponse(BaseModel):
    """Response model for health check"""
    status: str = Field(..., description="Service health status")
    models_loaded: bool = Field(..., description="Whether models are loaded")
    available_languages: List[str] = Field(..., description="Supported languages")
    
    class Config:
        schema_extra = {
            "example": {
                "status": "healthy",
                "models_loaded": True,
                "available_languages": ["en", "zh"]
            }
        }


class ErrorResponse(BaseModel):
    """Standard error response model"""
    error: str = Field(..., description="Error type")
    message: str = Field(..., description="Error message")
    details: Optional[Dict[str, Any]] = Field(None, description="Additional error details")
    
    class Config:
        schema_extra = {
            "example": {
                "error": "ValidationError",
                "message": "PDF URL is invalid",
                "details": {"url": "Not a valid URL"}
            }
        }
