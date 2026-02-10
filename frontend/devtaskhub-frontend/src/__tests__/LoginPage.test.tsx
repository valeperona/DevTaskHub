import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { LoginPage } from '../pages/LoginPage';
import { apiClient } from '../api/apiClient';

vi.mock('../api/apiClient', () => ({
  apiClient: {
    login: vi.fn(),
    register: vi.fn()
  }
}));

const mockedApi = vi.mocked(apiClient);

describe('LoginPage', () => {
  it('muestra error si los campos están vacíos al enviar', async () => {
    render(<LoginPage onLogin={vi.fn()} />);

    fireEvent.click(screen.getByRole('button', { name: /Ingresar/i }));

    expect(await screen.findByText(/Ingresá un email válido/i)).toBeInTheDocument();
  });

  it('llama onLogin al loguear correctamente', async () => {
    const onLogin = vi.fn();
    mockedApi.login.mockResolvedValue({ userId: '1', email: 'a@test.com', token: 'x' });

    render(<LoginPage onLogin={onLogin} />);
    await userEvent.type(screen.getByLabelText(/Email/i), 'a@test.com');
    await userEvent.type(screen.getByLabelText(/Contraseña/i), 'secret123');
    fireEvent.click(screen.getByRole('button', { name: /Ingresar/i }));

    await waitFor(() => expect(onLogin).toHaveBeenCalledWith({ userId: '1', email: 'a@test.com', token: 'x' }));
  });

  it('muestra mensaje de error si el login falla', async () => {
    mockedApi.login.mockRejectedValue(new Error('No se pudo iniciar sesión'));
    render(<LoginPage onLogin={vi.fn()} />);
    await userEvent.type(screen.getByLabelText(/Email/i), 'a@test.com');
    await userEvent.type(screen.getByLabelText(/Contraseña/i), 'secret123');
    fireEvent.click(screen.getByRole('button', { name: /Ingresar/i }));

    expect(await screen.findByText(/No se pudo iniciar sesión/i)).toBeInTheDocument();
  });
});
