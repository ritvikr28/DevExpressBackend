import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router-dom'
import { router } from './router.tsx'
import { fetchSetup } from '@devexpress/analytics-core/analytics-utils'
import './index.css'

// Configure DevExpress fetch authorization headers BEFORE any component renders.
// Using the direct ESM import of fetchSetup (from @devexpress/analytics-core) ensures
// this works in all module contexts without relying on the window.DevExpress global.
const earlyToken = localStorage.getItem('auth_token');
if (earlyToken) {
    fetchSetup.fetchSettings = {
        headers: { 'Authorization': `Bearer ${earlyToken}` }
    };
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
)
