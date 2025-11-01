import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { Navigation } from './components/Navigation';
import { Home } from './pages/Home';
import { Documentation } from './pages/Documentation';
import { QuickStart } from './pages/QuickStart';
import { CodeExamples } from './pages/CodeExamples';
import { PublishingGuidelines } from './pages/PublishingGuidelines';
import { PluginMarketplace } from './pages/PluginMarketplace';
import { DevelopmentTools } from './pages/DevelopmentTools';
import { IntegrationPatterns } from './pages/IntegrationPatterns';
import { Community } from './pages/Community';
import './App.css';

function App() {
  return (
    <Router>
      <div className="min-h-screen bg-gray-50">
        <Navigation />
        <main className="pt-16">
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/documentation" element={<Documentation />} />
            <Route path="/quick-start" element={<QuickStart />} />
            <Route path="/examples" element={<CodeExamples />} />
            <Route path="/publishing" element={<PublishingGuidelines />} />
            <Route path="/marketplace" element={<PluginMarketplace />} />
            <Route path="/tools" element={<DevelopmentTools />} />
            <Route path="/patterns" element={<IntegrationPatterns />} />
            <Route path="/community" element={<Community />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;