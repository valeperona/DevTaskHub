import { useState } from 'react';
import type { FormEvent } from 'react';
import styles from './LoginPage.module.css';

interface LoginPageProps {
  onLogin: (email: string) => void;
}

export function LoginPage({ onLogin }: LoginPageProps) {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!email.trim()) {
      setError('Ingresá un email válido');
      return;
    }
    setError(null);
    onLogin(email.trim().toLowerCase());
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
        />
        {error && <span className={styles.error}>{error}</span>}
        <button type="submit">Ingresar</button>
      </form>
    </div>
  );
}
