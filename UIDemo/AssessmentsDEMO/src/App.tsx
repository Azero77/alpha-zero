import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import AssessmentBuilder from './pages/AssessmentBuilder';
import QuizTaker from './pages/QuizTaker';
import ManualGrading from './pages/ManualGrading';

function App() {
  return (
    <Router>
      <div className="w-screen min-h-screen bg-slate-950 flex flex-col items-center">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/builder" element={<AssessmentBuilder />} />
          <Route path="/quiz/:id" element={<QuizTaker />} />
          <Route path="/grade/:id" element={<ManualGrading />} />
        </Routes>
      </div>
    </Router>
  );
}

export default App;
