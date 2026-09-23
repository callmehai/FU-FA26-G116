import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { App } from './app/App'
import { ToastProvider } from './components/ToastProvider'
import { AuthProvider } from './features/auth/AuthProvider'
import { installPressFeedback } from './lib/pressFeedback'
import './styles/global.css'

installPressFeedback()

const root = document.getElementById('root')
if (!root) throw new Error('Không tìm thấy #root trong index.html')

createRoot(root).render(
  <StrictMode>
    <BrowserRouter>
      <ToastProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </ToastProvider>
    </BrowserRouter>
  </StrictMode>,
)
