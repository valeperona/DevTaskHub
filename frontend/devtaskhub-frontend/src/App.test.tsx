import { MemoryRouter } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import App from './App';

describe('App', () => {
  it('renders the DevTaskHub login screen by default', () => {
    // Arrange: ensure no persisted session and render App within a router
    localStorage.removeItem('devtaskhub:user');

    // Act: render the root component
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>
    );

    // Assert: the brand/title should be visible on the login page
    expect(screen.getByRole('heading', { name: /devtaskhub/i })).toBeInTheDocument();
    expect(screen.getByText(/ingresá con tu email/i)).toBeInTheDocument();
  });
});
