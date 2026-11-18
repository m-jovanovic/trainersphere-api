import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';
import { App } from './app';
import { createAppRouter } from './router';
import { createQueryClient } from '@/lib/query-client';
import { registerMutationDefaults } from '@/features/session/offline';
import { applyTheme } from '@/lib/theme';

async function enableMocking() {
  if (import.meta.env.VITE_MOCK !== 'true') return;
  const { startMockWorker } = await import('./mocks/browser');
  await startMockWorker();
}

applyTheme();
await enableMocking();

const queryClient = createQueryClient();
registerMutationDefaults(queryClient);
const router = createAppRouter(queryClient);

const root = document.getElementById('root');
if (!root) throw new Error('#root missing');
createRoot(root).render(
  <StrictMode>
    <App queryClient={queryClient} router={router} />
  </StrictMode>,
);
