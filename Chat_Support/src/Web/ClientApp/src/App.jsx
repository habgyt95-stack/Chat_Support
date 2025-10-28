// src/App.jsx

import AppRouter from './AppRouter.jsx';
import './App.css';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient();

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      {/* BrowserRouter از اینجا حذف شد */}
  <AppRouter />
    </QueryClientProvider>
  );
}

export default App;