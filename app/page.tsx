'use client'
import { useState } from 'react'

interface SearchResult {
  id: string
  title: string
  authors: { name: string }[]
  abstract: string
  source: string
  year: number
  url: string
  score: number
}

export default function HomePage() {
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(false)
  const [results, setResults] = useState<SearchResult[]>([])
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!query.trim()) return
    
    setLoading(true)
    setError('')
    
    try {
      const response = await fetch('http://localhost:5182/api/search', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query: query.trim(), limit: 20 })
      })
      
      if (!response.ok) {
        throw new Error(`Search failed: ${response.status}`)
      }
      
      const data = await response.json()
      setResults(data.results || [])
    } catch (err) {
      console.error('Search error:', err)
      setError('Search failed. Make sure the backend is running.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div>
      <h1 className="page-title">Search Academic Papers</h1>

      <div className="search-container" style={{marginBottom: '2rem'}}>
        <form onSubmit={handleSubmit} style={{display: 'flex', flexDirection: 'column', gap: '1rem'}}>
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search academic papers..."
            className="search-input"
            required
          />
          <button
            type="submit"
            disabled={loading}
            className="search-button"
          >
            {loading ? 'Searching...' : 'Search'}
          </button>
        </form>
      </div>

      {error && (
        <div className="error-message">
          <p>{error}</p>
        </div>
      )}

      {results.length > 0 && (
        <div style={{marginTop: '2rem'}}>
          <h2 className="results-title">Found {results.length} papers</h2>
          
          {results.map((result) => (
            <div key={result.id} className="result-card">
              <h3 className="result-title">{result.title}</h3>
              <p className="result-authors">
                {result.authors.map(a => a.name).join(', ')} • {result.year} • {result.source}
              </p>
              <p className="result-abstract">{result.abstract}</p>
              {result.url && (
                <a href={result.url} target="_blank" className="view-paper-btn">
                  View Paper →
                </a>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}