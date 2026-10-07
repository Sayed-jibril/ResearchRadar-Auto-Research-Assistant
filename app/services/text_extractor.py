"""
PDF text extraction service using pdfminer.six
"""
import os
import logging
import tempfile
import asyncio
from typing import Tuple, Optional
from pathlib import Path
from urllib.parse import urlparse

import httpx
from pdfminer.high_level import extract_text
from pdfminer.pdfparser import PDFSyntaxError

from app.core.config import settings

logger = logging.getLogger(__name__)


class TextExtractor:
    """Service for extracting text from PDF files"""
    
    def __init__(self):
        # Ensure temp directory exists
        os.makedirs(settings.TEMP_DIR, exist_ok=True)
    
    async def extract_from_url(self, pdf_url: str) -> Tuple[str, int]:
        """
        Extract text from a PDF URL
        
        Args:
            pdf_url: URL to the PDF file
            
        Returns:
            Tuple of (extracted_text, character_count)
            
        Raises:
            ValueError: If URL is invalid or PDF cannot be processed
            TimeoutError: If download/processing takes too long
        """
        # Validate URL
        parsed_url = urlparse(pdf_url)
        if not all([parsed_url.scheme, parsed_url.netloc]):
            raise ValueError(f"Invalid URL: {pdf_url}")
        
        # Download PDF
        pdf_path = await self._download_pdf(pdf_url)
        
        try:
            # Extract text
            text = await self._extract_text_from_file(pdf_path)
            
            # Validate extracted text
            if not text or len(text.strip()) < 100:
                raise ValueError("PDF appears to be empty or text extraction failed")
            
            if len(text) > settings.MAX_TEXT_LENGTH:
                logger.warning(f"Text too long ({len(text)} chars), truncating to {settings.MAX_TEXT_LENGTH}")
                text = text[:settings.MAX_TEXT_LENGTH]
            
            return text, len(text)
            
        finally:
            # Clean up temporary file
            try:
                os.unlink(pdf_path)
            except OSError:
                pass
    
    async def _download_pdf(self, url: str) -> str:
        """Download PDF from URL to temporary file"""
        try:
            async with httpx.AsyncClient(timeout=60.0) as client:
                logger.info(f"Downloading PDF from: {url}")
                
                response = await client.get(url, follow_redirects=True)
                response.raise_for_status()
                
                # Check content type
                content_type = response.headers.get("content-type", "").lower()
                if "pdf" not in content_type:
                    logger.warning(f"Unexpected content type: {content_type}")
                
                # Check file size
                content_length = int(response.headers.get("content-length", 0))
                max_size_bytes = settings.MAX_PDF_SIZE_MB * 1024 * 1024
                
                if content_length > max_size_bytes:
                    raise ValueError(f"PDF too large: {content_length / 1024 / 1024:.1f}MB (max: {settings.MAX_PDF_SIZE_MB}MB)")
                
                # Save to temporary file
                with tempfile.NamedTemporaryFile(
                    delete=False, 
                    suffix=".pdf", 
                    dir=settings.TEMP_DIR
                ) as temp_file:
                    temp_file.write(response.content)
                    temp_path = temp_file.name
                
                logger.info(f"PDF downloaded to: {temp_path}")
                return temp_path
                
        except httpx.HTTPError as e:
            logger.error(f"Failed to download PDF: {e}")
            raise ValueError(f"Failed to download PDF: {e}")
        except Exception as e:
            logger.error(f"Unexpected error downloading PDF: {e}")
            raise
    
    async def _extract_text_from_file(self, pdf_path: str) -> str:
        """Extract text from a local PDF file"""
        try:
            # Run extraction in thread pool to avoid blocking
            loop = asyncio.get_event_loop()
            
            def extract():
                try:
                    # Extract text using pdfminer
                    text = extract_text(pdf_path)
                    
                    # Clean up the text
                    text = self._clean_text(text)
                    
                    return text
                    
                except PDFSyntaxError as e:
                    logger.error(f"PDF syntax error: {e}")
                    raise ValueError(f"Invalid PDF format: {e}")
                except Exception as e:
                    logger.error(f"Text extraction failed: {e}")
                    raise ValueError(f"Text extraction failed: {e}")
            
            return await loop.run_in_executor(None, extract)
            
        except Exception as e:
            logger.error(f"Failed to extract text from {pdf_path}: {e}")
            raise
    
    def _clean_text(self, text: str) -> str:
        """Clean and normalize extracted text"""
        if not text:
            return ""
        
        # Remove excessive whitespace
        lines = []
        for line in text.split('\n'):
            line = line.strip()
            if line:
                lines.append(line)
        
        # Join with single spaces
        cleaned = ' '.join(lines)
        
        # Remove multiple consecutive spaces
        import re
        cleaned = re.sub(r'\s+', ' ', cleaned)
        
        return cleaned.strip()
    
    async def health_check(self) -> bool:
        """Check if text extraction service is working"""
        try:
            # Create a minimal test PDF in memory (or use a known good URL)
            # For now, just check if temp directory is accessible
            test_file = os.path.join(settings.TEMP_DIR, "health_check.txt")
            
            with open(test_file, 'w') as f:
                f.write("test")
            
            os.unlink(test_file)
            return True
            
        except Exception as e:
            logger.error(f"Text extractor health check failed: {e}")
            return False
