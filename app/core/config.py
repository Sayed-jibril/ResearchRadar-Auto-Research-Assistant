"""
Configuration settings for the NLP service
"""
import os
from typing import Optional
from pydantic import BaseSettings


class Settings(BaseSettings):
    """Application settings loaded from environment variables"""
    
    # Server settings
    HOST: str = "0.0.0.0"
    PORT: int = 8000
    DEBUG: bool = False
    LOG_LEVEL: str = "INFO"
    
    # Model settings
    MODEL_CACHE_DIR: str = "/app/models"
    TEMP_DIR: str = "/app/temp"
    
    # English summarization model
    EN_SUMMARIZER_MODEL: str = "facebook/bart-large-cnn"
    EN_SUMMARIZER_MODEL_SMALL: str = "sshleifer/distilbart-cnn-12-6"
    
    # Chinese summarization model
    ZH_SUMMARIZER_MODEL: str = "csebuetnlp/mT5_multilingual_XLSum"
    
    # Model settings
    MAX_INPUT_LENGTH: int = 1024
    MAX_OUTPUT_LENGTH: int = 512
    CHUNK_SIZE: int = 1500
    CHUNK_OVERLAP: int = 100
    
    # Processing limits
    MAX_PDF_SIZE_MB: int = 50
    MAX_TEXT_LENGTH: int = 1000000  # 1M characters
    REQUEST_TIMEOUT_SECONDS: int = 300  # 5 minutes
    
    # Use small models for development/low-memory environments
    USE_SMALL_MODELS: bool = False
    
    class Config:
        env_file = ".env"
        case_sensitive = True


# Create global settings instance
settings = Settings()
