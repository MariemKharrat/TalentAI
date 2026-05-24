import { NavLink } from 'react-router-dom';
import { useTheme } from '../ThemeContext';
import logo from '../logo.svg';

const navItems = [
  { to: '/', label: 'Dashboard' },
  { to: '/candidates', label: 'Candidates' },
  { to: '/jobs', label: 'Jobs' },
];

function Navbar() {
  const { theme, toggleTheme } = useTheme();

  return (
    <header className="navbar">
      <div className="navbar-inner">
        <NavLink to="/" className="brand">
          <img src={logo} alt="TalentAI" className="brand-logo" />
          TalentAI
          <span className="brand-badge">
            <span className="brand-badge-dot" />
            AI Active
          </span>
        </NavLink>
        <nav className="nav-links" aria-label="Main navigation">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) => `nav-link${isActive ? ' nav-link-active' : ''}`}
            >
              {item.label}
            </NavLink>
          ))}
          <button
            className="theme-toggle"
            onClick={toggleTheme}
            aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
            title={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
          >
            {theme === 'dark' ? '☀️' : '🌙'}
          </button>
        </nav>
      </div>
    </header>
  );
}

export default Navbar;
