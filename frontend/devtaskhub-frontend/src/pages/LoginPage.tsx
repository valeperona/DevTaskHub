import { useState } from 'react';
import type { FormEvent } from 'react';
import { apiClient } from '../api/apiClient';
import type { AuthResponse } from '../api/apiClient';
import styles from './LoginPage.module.css';

interface LoginPageProps {
  onLogin: (auth: AuthResponse) => void;
}

export function LoginPage({ onLogin }: LoginPageProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!email.trim()) {
      setError('Ingresá un email válido');
      return;
    }
    if (!password.trim()) {
      setError('Ingresá tu contraseña');
      return;
    }

    setLoading(true);
    try {
      const session = await apiClient.login({ email: email.trim().toLowerCase(), password: password.trim() });
      setError(null);
      onLogin(session);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo iniciar sesión');
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async () => {
    if (!email.trim() || !password.trim()) {
      setError('Completá email y contraseña');
      return;
    }
    setLoading(true);
    try {
      const session = await apiClient.register({ email: email.trim().toLowerCase(), password: password.trim() });
      setError(null);
      onLogin(session);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear la cuenta');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.wrapper}>
      <h1 className={styles.title}>DevTaskHub</h1>
      <p className={styles.subtitle}>Ingresá con tu email para continuar</p>
      <form onSubmit={handleSubmit}>
        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          placeholder="nombre@ejemplo.com"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          data-cy="login-email-input"
        />
        <label htmlFor="password">Contraseña</label>
        <input
          id="password"
          type="password"
          placeholder="********"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          data-cy="login-password-input"
        />
        {error && <span className={styles.error}>{error}</span>}
        <div className={styles.actions}>
          <button type="submit" data-cy="login-submit-button" disabled={loading}>
            {loading ? 'Ingresando...' : 'Ingresar'}
          </button>
          <button type="button" onClick={handleRegister} className={styles.secondary} disabled={loading}>
            Crear cuenta
          </button>
        </div>
      </form>
    </div>
  );
}
