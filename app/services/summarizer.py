"""
Text summarization service using transformer models
"""
import logging
import asyncio
import re
from typing import List, Dict, Any, Tuple
from dataclasses import dataclass

import nltk
from rake_nltk import Rake

from app.core.config import settings
from app.services.model_manager import ModelManager

logger = logging.getLogger(__name__)

# Download required NLTK data
try:
    nltk.data.find('tokenizers/punkt')
except LookupError:
    nltk.download('punkt', quiet=True)

try:
    nltk.data.find('corpora/stopwords')
except LookupError:
    nltk.download('stopwords', quiet=True)


@dataclass
class SummaryResult:
    """Structured summary result"""
    tl_dr: str
    key_points: List[str]
    methods: str
    results: str
    limitations: List[str]


class TextSummarizer:
    """Service for summarizing academic papers"""
    
    def __init__(self, model_manager: ModelManager):
        self.model_manager = model_manager
        
        # Initialize RAKE for keyword extraction
        self.rake_en = Rake()
        
    async def summarize(
        self, 
        text: str, 
        style: str = "technical", 
        language: str = "en",
        max_tokens: int = 1200
    ) -> SummaryResult:
        """
        Summarize text with structured output
        
        Args:
            text: Input text to summarize
            style: Summary style ("technical" or "executive")
            language: Text language ("en" or "zh")
            max_tokens: Maximum tokens for summary
            
        Returns:
            SummaryResult with structured summary components
        """
        # Get appropriate model
        summarizer = self.model_manager.get_summarizer(language)
        if not summarizer:
            raise ValueError(f"No summarizer available for language: {language}")
        
        # Split text into chunks for processing
        chunks = self._chunk_text(text, language)
        
        # Summarize each chunk
        chunk_summaries = []
        for chunk in chunks:
            chunk_summary = await self._summarize_chunk(chunk, summarizer, style)
            chunk_summaries.append(chunk_summary)
        
        # Combine and structure the summaries
        return await self._structure_summary(text, chunk_summaries, style, language)
    
    def _chunk_text(self, text: str, language: str = "en") -> List[str]:
        """Split text into manageable chunks for summarization"""
        # Use NLTK sentence tokenizer
        try:
            sentences = nltk.sent_tokenize(text, language='english' if language == 'en' else 'german')
        except Exception:
            # Fallback to simple splitting
            sentences = re.split(r'[.!?]+', text)
            sentences = [s.strip() for s in sentences if s.strip()]
        
        chunks = []
        current_chunk = ""
        
        for sentence in sentences:
            # Estimate tokens (rough approximation: 1 token ≈ 4 characters)
            estimated_tokens = len(current_chunk + " " + sentence) // 4
            
            if estimated_tokens > settings.CHUNK_SIZE and current_chunk:
                chunks.append(current_chunk.strip())
                current_chunk = sentence
            else:
                current_chunk += " " + sentence if current_chunk else sentence
        
        if current_chunk.strip():
            chunks.append(current_chunk.strip())
        
        return chunks
    
    async def _summarize_chunk(self, chunk: str, summarizer, style: str) -> str:
        """Summarize a single text chunk"""
        try:
            # Adjust parameters based on style
            if style == "executive":
                max_length = 100
                min_length = 30
            else:  # technical
                max_length = 200
                min_length = 50
            
            # Run summarization in thread pool
            loop = asyncio.get_event_loop()
            
            def summarize():
                result = summarizer(
                    chunk,
                    max_length=max_length,
                    min_length=min_length,
                    do_sample=False,
                    truncation=True
                )
                return result[0]['summary_text']
            
            summary = await loop.run_in_executor(None, summarize)
            return summary
            
        except Exception as e:
            logger.error(f"Failed to summarize chunk: {e}")
            # Return first few sentences as fallback
            sentences = chunk.split('. ')
            return '. '.join(sentences[:2]) + '.'
    
    async def _structure_summary(
        self, 
        original_text: str, 
        chunk_summaries: List[str], 
        style: str, 
        language: str
    ) -> SummaryResult:
        """Structure the summary into organized components"""
        
        # Combine chunk summaries
        combined_summary = " ".join(chunk_summaries)
        
        # Extract key phrases
        key_phrases = self._extract_key_phrases(original_text, language)
        
        # Generate TL;DR (very brief summary)
        tl_dr = await self._generate_tldr(combined_summary, language)
        
        # Extract structured components
        methods = self._extract_methods(original_text, combined_summary)
        results = self._extract_results(original_text, combined_summary)
        limitations = self._extract_limitations(original_text, combined_summary)
        
        return SummaryResult(
            tl_dr=tl_dr,
            key_points=key_phrases[:8],  # Top 8 key points
            methods=methods,
            results=results,
            limitations=limitations
        )
    
    def _extract_key_phrases(self, text: str, language: str) -> List[str]:
        """Extract key phrases using RAKE algorithm"""
        try:
            if language == "en":
                self.rake_en.extract_keywords_from_text(text)
                phrases = self.rake_en.get_ranked_phrases()
                
                # Filter and clean phrases
                clean_phrases = []
                for phrase in phrases[:15]:  # Top 15 phrases
                    if len(phrase.split()) <= 4 and len(phrase) > 3:  # Reasonable length
                        clean_phrases.append(phrase.title())
                
                return clean_phrases[:8]
            else:
                # Fallback for non-English: extract noun phrases or frequent terms
                return self._extract_noun_phrases_fallback(text)
                
        except Exception as e:
            logger.error(f"Key phrase extraction failed: {e}")
            return ["Key phrase extraction unavailable"]
    
    def _extract_noun_phrases_fallback(self, text: str) -> List[str]:
        """Fallback method for extracting important terms"""
        # Simple frequency-based extraction
        words = re.findall(r'\b[A-Z][a-z]+\b', text)  # Capitalized words
        word_freq = {}
        
        for word in words:
            if len(word) > 3:  # Skip short words
                word_freq[word] = word_freq.get(word, 0) + 1
        
        # Sort by frequency and return top terms
        sorted_words = sorted(word_freq.items(), key=lambda x: x[1], reverse=True)
        return [word for word, freq in sorted_words[:8] if freq > 1]
    
    async def _generate_tldr(self, summary: str, language: str) -> str:
        """Generate a very brief TL;DR summary"""
        # If summary is already short, use it
        if len(summary.split()) <= 60:
            return summary
        
        # Otherwise, try to get the most important sentence(s)
        sentences = re.split(r'[.!?]+', summary)
        sentences = [s.strip() for s in sentences if s.strip()]
        
        if not sentences:
            return "Summary unavailable"
        
        # Take first sentence or combine first two if they're short
        first_sentence = sentences[0]
        if len(first_sentence.split()) <= 40 and len(sentences) > 1:
            second_sentence = sentences[1]
            if len((first_sentence + " " + second_sentence).split()) <= 60:
                return f"{first_sentence}. {second_sentence}."
        
        return first_sentence + "."
    
    def _extract_methods(self, original_text: str, summary: str) -> str:
        """Extract methodology information"""
        method_keywords = [
            "method", "approach", "technique", "algorithm", "framework",
            "procedure", "methodology", "protocol", "experiment", "analysis"
        ]
        
        # Look for method-related sentences in summary first, then original
        for text_source in [summary, original_text]:
            sentences = re.split(r'[.!?]+', text_source)
            method_sentences = []
            
            for sentence in sentences:
                sentence = sentence.strip()
                if any(keyword in sentence.lower() for keyword in method_keywords):
                    method_sentences.append(sentence)
                    if len(" ".join(method_sentences).split()) > 100:  # Limit length
                        break
            
            if method_sentences:
                return ". ".join(method_sentences[:3]) + "."
        
        return "Methodology details not clearly identified in the text."
    
    def _extract_results(self, original_text: str, summary: str) -> str:
        """Extract results and findings"""
        result_keywords = [
            "result", "finding", "outcome", "conclusion", "demonstrate",
            "show", "achieve", "performance", "accuracy", "improvement"
        ]
        
        for text_source in [summary, original_text]:
            sentences = re.split(r'[.!?]+', text_source)
            result_sentences = []
            
            for sentence in sentences:
                sentence = sentence.strip()
                if any(keyword in sentence.lower() for keyword in result_keywords):
                    result_sentences.append(sentence)
                    if len(" ".join(result_sentences).split()) > 100:
                        break
            
            if result_sentences:
                return ". ".join(result_sentences[:3]) + "."
        
        return "Specific results not clearly identified in the text."
    
    def _extract_limitations(self, original_text: str, summary: str) -> List[str]:
        """Extract limitations and future work"""
        limitation_keywords = [
            "limitation", "limit", "constraint", "drawback", "challenge",
            "future work", "further research", "improvement", "weakness"
        ]
        
        limitations = []
        
        for text_source in [summary, original_text]:
            sentences = re.split(r'[.!?]+', text_source)
            
            for sentence in sentences:
                sentence = sentence.strip()
                if any(keyword in sentence.lower() for keyword in limitation_keywords):
                    limitations.append(sentence)
                    if len(limitations) >= 5:  # Max 5 limitations
                        break
            
            if limitations:
                break
        
        if not limitations:
            limitations = ["Limitations not explicitly discussed in the available text."]
        
        return limitations[:5]
    
    async def health_check(self) -> bool:
        """Check if summarization service is working"""
        try:
            test_text = """
            This is a test document for health checking the summarization service.
            The document contains multiple sentences to test the chunking and summarization pipeline.
            It should be processed without errors and return a structured summary.
            The methodology involves testing each component of the summarization pipeline.
            Results show that the system can process text and generate summaries.
            Limitations include the artificial nature of this test document.
            """
            
            result = await self.summarize(test_text, style="technical", language="en")
            
            # Verify all required fields are present and non-empty
            return (
                bool(result.tl_dr.strip()) and
                bool(result.key_points) and
                bool(result.methods.strip()) and
                bool(result.results.strip()) and
                bool(result.limitations)
            )
            
        except Exception as e:
            logger.error(f"Summarizer health check failed: {e}")
            return False
