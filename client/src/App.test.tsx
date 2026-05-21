import { render, screen, within } from '@testing-library/react';
import App from './App';

test('renders recruitment dashboard navigation', () => {
  render(<App />);
  expect(screen.getByText(/Recruitment AI Career App/i)).toBeInTheDocument();

  const navigation = screen.getByRole('navigation', { name: /main navigation/i });
  expect(within(navigation).getByRole('link', { name: /Candidates/i })).toBeInTheDocument();
  expect(within(navigation).getByRole('link', { name: /Jobs/i })).toBeInTheDocument();
});
