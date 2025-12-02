import { useEffect, useState } from 'react';
import { apiClient } from '../api/apiClient';
import type { ProjectInvitation } from '../api/apiClient';
import styles from './InvitationsPage.module.css';

export function InvitationsPage() {
  const [invitations, setInvitations] = useState<ProjectInvitation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const data = await apiClient.getMyInvitations();
      setInvitations(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron cargar las invitaciones');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const handleRespond = async (id: string, status: 'Accepted' | 'Declined') => {
    try {
      await apiClient.respondInvitation(id, status);
      setToast(status === 'Accepted' ? 'Invitación aceptada' : 'Invitación rechazada');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo responder la invitación');
    }
  };

  if (loading) return <p className={styles.meta}>Cargando invitaciones...</p>;

  return (
    <div className={styles.wrapper}>
      <div className={styles.header}>
        <h2>Invitaciones pendientes</h2>
        <p className={styles.meta}>Acepta o rechaza el acceso a proyectos</p>
      </div>
      {toast && <div className={styles.toast}>{toast}</div>}
      {error && <div className={styles.alert}>{error}</div>}
      {invitations.length === 0 ? (
        <p className={styles.meta}>No tienes invitaciones pendientes</p>
      ) : (
        <div className={styles.list}>
          {invitations.map((inv) => (
            <div key={inv.id} className={styles.card}>
              <div className={styles.row}>
                <strong>{inv.projectName || inv.projectId}</strong>
                <span className={styles.badge}>{inv.role}</span>
              </div>
              <p className={styles.meta}>Invitado como {inv.role}</p>
              <div className={styles.actions}>
                <button onClick={() => handleRespond(inv.id, 'Accepted')} className={styles.primary}>
                  Aceptar
                </button>
                <button onClick={() => handleRespond(inv.id, 'Declined')} className={styles.secondary}>
                  Rechazar
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
