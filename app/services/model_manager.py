"""
Model manager for loading and managing ML models
"""
import os
import logging
from typing import Dict, Any, Optional
import asyncio
from pathlib import Path

import torch
from transformers import (
    AutoTokenizer, 
    AutoModelForSeq2SeqLM,
    pipeline,
    Pipeline
)

from app.core.config import settings

logger = logging.getLogger(__name__)


class ModelManager:
    """Manages loading and access to ML models"""
    
    def __init__(self):
        self.models: Dict[str, Pipeline] = {}
        self.tokenizers: Dict[str, Any] = {}
        self._initialized = False
        
        # Ensure model cache directory exists
        os.makedirs(settings.MODEL_CACHE_DIR, exist_ok=True)
        
    async def initialize(self) -> None:
        """Initialize and load all required models"""
        if self._initialized:
            return
            
        logger.info("Initializing models...")
        
        try:
            # Load English summarization model
            await self._load_en_summarizer()
            
            # Load Chinese summarization model (if needed)
            # await self._load_zh_summarizer()
            
            self._initialized = True
            logger.info("All models loaded successfully")
            
        except Exception as e:
            logger.error(f"Failed to initialize models: {e}")
            raise
    
    async def _load_en_summarizer(self) -> None:
        """Load English summarization model"""
        model_name = (
            settings.EN_SUMMARIZER_MODEL_SMALL 
            if settings.USE_SMALL_MODELS 
            else settings.EN_SUMMARIZER_MODEL
        )
        
        logger.info(f"Loading English summarizer: {model_name}")
        
        try:
            # Run in thread pool to avoid blocking
            loop = asyncio.get_event_loop()
            
            def load_model():
                return pipeline(
                    "summarization",
                    model=model_name,
                    tokenizer=model_name,
                    device=0 if torch.cuda.is_available() else -1,  # Use GPU if available
                    cache_dir=settings.MODEL_CACHE_DIR,
                    truncation=True,
                    max_length=settings.MAX_OUTPUT_LENGTH,
                    min_length=30,
                    do_sample=False
                )
            
            self.models["en_summarizer"] = await loop.run_in_executor(
                None, load_model
            )
            
            logger.info("English summarizer loaded successfully")
            
        except Exception as e:
            logger.error(f"Failed to load English summarizer: {e}")
            raise
    
    async def _load_zh_summarizer(self) -> None:
        """Load Chinese summarization model"""
        model_name = settings.ZH_SUMMARIZER_MODEL
        
        logger.info(f"Loading Chinese summarizer: {model_name}")
        
        try:
            loop = asyncio.get_event_loop()
            
            def load_model():
                return pipeline(
                    "summarization",
                    model=model_name,
                    tokenizer=model_name,
                    device=0 if torch.cuda.is_available() else -1,
                    cache_dir=settings.MODEL_CACHE_DIR,
                    truncation=True,
                    max_length=settings.MAX_OUTPUT_LENGTH,
                    min_length=30,
                    do_sample=False
                )
            
            self.models["zh_summarizer"] = await loop.run_in_executor(
                None, load_model
            )
            
            logger.info("Chinese summarizer loaded successfully")
            
        except Exception as e:
            logger.error(f"Failed to load Chinese summarizer: {e}")
            # Don't raise - Chinese support is optional for now
            logger.warning("Chinese summarization will not be available")
    
    def get_summarizer(self, language: str = "en") -> Optional[Pipeline]:
        """Get summarization model for specified language"""
        model_key = f"{language}_summarizer"
        return self.models.get(model_key)
    
    async def health_check(self) -> bool:
        """Check if models are loaded and healthy"""
        try:
            # Check if at least English summarizer is available
            en_summarizer = self.get_summarizer("en")
            if not en_summarizer:
                return False
                
            # Quick test summarization
            test_text = "This is a test sentence for health check. The model should be able to process this text without errors."
            
            loop = asyncio.get_event_loop()
            
            def test_model():
                try:
                    result = en_summarizer(test_text, max_length=20, min_length=5)
                    return len(result) > 0 and 'summary_text' in result[0]
                except Exception as e:
                    logger.error(f"Model health check failed: {e}")
                    return False
            
            return await loop.run_in_executor(None, test_model)
            
        except Exception as e:
            logger.error(f"Health check error: {e}")
            return False
    
    async def cleanup(self) -> None:
        """Cleanup resources"""
        logger.info("Cleaning up model manager...")
        
        # Clear model references
        self.models.clear()
        self.tokenizers.clear()
        
        # Force garbage collection
        import gc
        gc.collect()
        
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
        
        logger.info("Model manager cleanup complete")
    
    @property
    def is_initialized(self) -> bool:
        """Check if manager is initialized"""
        return self._initialized
    
    @property
    def available_languages(self) -> list:
        """Get list of available languages"""
        languages = []
        if "en_summarizer" in self.models:
            languages.append("en")
        if "zh_summarizer" in self.models:
            languages.append("zh")
        return languages
