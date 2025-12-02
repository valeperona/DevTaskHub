import { Navigate, Route, Routes } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { LoginPage } from './pages/LoginPage';
import { ProjectsPage } from './pages/ProjectsPage';
import { ProjectBoardPage } from './pages/ProjectBoardPage';
import { MyTasksPage } from './pages/MyTasksPage';
import { CalendarPage } from './pages/CalendarPage';
import { InvitationsPage } from './pages/InvitationsPage';
import type { AuthResponse } from './api/apiClient';
import { setAuthToken } from './api/apiClient';
import styles from './App.module.css';

function ProtectedRoute({ children, isAuthenticated }: { children: React.ReactElement; isAuthenticated: boolean }) {
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  return children;
}

function App() {
  const [session, setSession] = useState<AuthResponse | null>(() => {
    const stored = localStorage.getItem('devtaskhub:session');
    return stored ? (JSON.parse(stored) as AuthResponse) : null;
  });

  useEffect(() => {
    if (session) {
      localStorage.setItem('devtaskhub:session', JSON.stringify(session));
      setAuthToken(session.token);
    } else {
      localStorage.removeItem('devtaskhub:session');
      setAuthToken(null);
    }
  }, [session]);

  const handleLogin = (auth: AuthResponse) => {
    setSession(auth);
  };

  const handleLogout = () => {
    setSession(null);
  };

  return (
    <div className={styles.appShell}>
      {session && (
        <header className={styles.header}>
          <h1>DevTaskHub</h1>
          <div className={styles.userInfo}>
            <span>{session.email}</span>
            <nav className={styles.nav}>
              <a href="/">Proyectos</a>
              <a href="/my-tasks">Mis tareas</a>
              <a href="/calendar">Calendario</a>
              <a href="/invitations">Invitaciones</a>
            </nav>
            <button className={styles.logoutButton} onClick={handleLogout}>
              Salir
            </button>
          </div>
        </header>
      )}
      <main className={styles.content}>
        <Routes>
          <Route
            path="/login"
            element={session ? <Navigate to="/" replace /> : <LoginPage onLogin={handleLogin} />}
          />
          <Route
            path="/"
            element={
              <ProtectedRoute isAuthenticated={!!session}>
                <ProjectsPage session={session!} />
              </ProtectedRoute>
            }
          />
          <Route
            path="/my-tasks"
            element={
              <ProtectedRoute isAuthenticated={!!session}>
                <MyTasksPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/calendar"
            element={
              <ProtectedRoute isAuthenticated={!!session}>
                <CalendarPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/invitations"
            element={
              <ProtectedRoute isAuthenticated={!!session}>
                <InvitationsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/projects/:projectId"
            element={
              <ProtectedRoute isAuthenticated={!!session}>
                <ProjectBoardPage />
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
