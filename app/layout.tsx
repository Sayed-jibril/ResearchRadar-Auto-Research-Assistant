import "./globals.css"

export const metadata = {
  title: 'ScholarLens - AI Research Assistant',
  description: 'Search and analyze academic papers with AI',
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en">
      <body style={{minHeight: '100vh', display: 'flex', flexDirection: 'column'}}>
        {/* Header */}
        <header className="header">
          <div style={{maxWidth: '1200px', margin: '0 auto', display: 'flex', alignItems: 'center', justifyContent: 'space-between'}}>
            <div style={{display: 'flex', alignItems: 'center', gap: '12px'}}>
              <img src="/logo.png" alt="ScholarLens" style={{height: '32px', width: '32px', borderRadius: '6px'}} />
              <span style={{color: 'white', fontWeight: 'bold', fontSize: '18px'}}>ScholarLens</span>
            </div>
            <nav style={{display: 'flex', alignItems: 'center', gap: '24px', fontSize: '14px'}}>
              <a href="#" style={{color: '#d1d5db', textDecoration: 'none'}}>Home</a>
              <a href="#" style={{color: '#d1d5db', textDecoration: 'none'}}>About</a>
              <a href="#" style={{color: '#d1d5db', textDecoration: 'none'}}>Contact</a>
            </nav>
          </div>
        </header>

        {/* Main content */}
        <main style={{flex: '1', padding: '32px 16px'}}>
          <div style={{maxWidth: '800px', margin: '0 auto'}}>{children}</div>
        </main>

        {/* Footer */}
        <footer className="footer">
          <p>© 2025 ScholarLens · All rights reserved.</p>
        </footer>
      </body>
    </html>
  )
}