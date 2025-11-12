import { Navigate, Route, Routes } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { LoginPage } from './pages/LoginPage';
import { ProjectsPage } from './pages/ProjectsPage';
import { ProjectBoardPage } from './pages/ProjectBoardPage';
import styles from './App.module.css';

function ProtectedRoute({ children, isAuthenticated }: { children: React.ReactElement; isAuthenticated: boolean }) {
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  return children;
}

function App() {
  const [userEmail, setUserEmail] = useState<string | null>(() => localStorage.getItem('devtaskhub:user'));

  useEffect(() => {
    if (userEmail) {
      localStorage.setItem('devtaskhub:user', userEmail);
    } else {
      localStorage.removeItem('devtaskhub:user');
    }
  }, [userEmail]);

  const handleLogin = (email: string) => {
    setUserEmail(email);
  };

  const handleLogout = () => {
    setUserEmail(null);
  };

  return (
    <div className={styles.appShell}>
      {userEmail && (
        <header className={styles.header}>
          <h1>DevTaskHub</h1>
          <div className={styles.userInfo}>
            <span>{userEmail}</span>
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
            element={userEmail ? <Navigate to="/" replace /> : <LoginPage onLogin={handleLogin} />}
          />
          <Route
            path="/"
            element={
              <ProtectedRoute isAuthenticated={!!userEmail}>
                <ProjectsPage userEmail={userEmail ?? ''} />
              </ProtectedRoute>
            }
          />
          <Route
            path="/projects/:projectId"
            element={
              <ProtectedRoute isAuthenticated={!!userEmail}>
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
