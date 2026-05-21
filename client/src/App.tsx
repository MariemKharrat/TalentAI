import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import Navbar from './components/Navbar';
import './App.css';
import CandidateDetail from './pages/CandidateDetail';
import CandidatesPage from './pages/CandidatesPage';
import CreateJob from './pages/CreateJob';
import Dashboard from './pages/Dashboard';
import JobDetail from './pages/JobDetail';
import JobsPage from './pages/JobsPage';

function App() {
  return (
    <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <div className="app-shell">
        <Navbar />
        <main className="page-container">
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/candidates" element={<CandidatesPage />} />
            <Route path="/candidates/:id" element={<CandidateDetail />} />
            <Route path="/jobs" element={<JobsPage />} />
            <Route path="/jobs/create" element={<CreateJob />} />
            <Route path="/jobs/:id" element={<JobDetail />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}

export default App;
