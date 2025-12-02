import { useEffect, useMemo, useState } from 'react';
import { apiClient } from '../api/apiClient';
import type { TaskItem } from '../api/apiClient';
import styles from './CalendarPage.module.css';

type ViewMode = 'month' | 'week';

function isOverdue(task: TaskItem) {
  if (!task.dueDate) return false;
  const due = new Date(task.dueDate).getTime();
  return task.status !== 'Done' && due < Date.now();
}

function startOfWeek(date: Date) {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  const day = d.getUTCDay();
  const diff = (day + 7 - 1) % 7; // start Monday
  d.setUTCDate(d.getUTCDate() - diff);
  return d;
}

function startOfMonth(date: Date) {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1));
}

function addDays(date: Date, days: number) {
  const d = new Date(date);
  d.setUTCDate(d.getUTCDate() + days);
  return d;
}

export function CalendarPage() {
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [mode, setMode] = useState<ViewMode>('month');
  const [anchor, setAnchor] = useState(() => new Date());

  const load = async () => {
    setLoading(true);
    try {
      const from = mode === 'week' ? startOfWeek(anchor) : startOfMonth(anchor);
      const to =
        mode === 'week'
          ? addDays(from, 6)
          : new Date(Date.UTC(from.getUTCFullYear(), from.getUTCMonth() + 1, 0));
      const data = await apiClient.getCalendarTasks({
        from: from.toISOString(),
        to: to.toISOString()
      });
      setTasks(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar el calendario');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [mode, anchor]);

  const days = useMemo(() => {
    const slots: { date: Date; tasks: TaskItem[] }[] = [];
    const start = mode === 'week' ? startOfWeek(anchor) : startOfMonth(anchor);
    const total = mode === 'week' ? 7 : new Date(start.getUTCFullYear(), start.getUTCMonth() + 1, 0).getUTCDate();

    for (let i = 0; i < total; i++) {
      const day = addDays(start, i);
      const dayKey = day.toDateString();
      const dayTasks = tasks.filter((t) => t.dueDate && new Date(t.dueDate).toDateString() === dayKey);
      slots.push({ date: day, tasks: dayTasks });
    }
    return slots;
  }, [tasks, mode, anchor]);

  const goPrev = () => {
    setAnchor((prev) => (mode === 'week' ? addDays(prev, -7) : new Date(prev.getFullYear(), prev.getMonth() - 1, 1)));
  };
  const goNext = () => {
    setAnchor((prev) => (mode === 'week' ? addDays(prev, 7) : new Date(prev.getFullYear(), prev.getMonth() + 1, 1)));
  };

  return (
    <div className={styles.wrapper}>
      <div className={styles.header}>
        <div>
          <h2>Calendario de vencimientos</h2>
          <p className={styles.meta}>{mode === 'week' ? 'Vista semanal' : 'Vista mensual'}</p>
        </div>
        <div className={styles.controls}>
          <button onClick={goPrev}>◀</button>
          <button onClick={goNext}>▶</button>
          <select value={mode} onChange={(e) => setMode(e.target.value as ViewMode)}>
            <option value="month">Mes</option>
            <option value="week">Semana</option>
          </select>
        </div>
      </div>

      {loading && <p className={styles.meta}>Cargando...</p>}
      {error && <p className={styles.error}>{error}</p>}

      <div className={styles.grid}>
        {days.map((slot) => (
          <div key={slot.date.toISOString()} className={styles.day}>
            <div className={styles.dayHeader}>
              <span>{slot.date.toLocaleDateString()}</span>
              {slot.tasks.length > 0 && <span className={styles.badge}>{slot.tasks.length}</span>}
            </div>
            {slot.tasks.length === 0 ? (
              <p className={styles.empty}>Sin tareas</p>
            ) : (
              slot.tasks.map((task) => (
                <div key={task.id} className={`${styles.card} ${isOverdue(task) ? styles.overdue : ''}`}>
                  <div className={styles.cardHeader}>
                    <span className={styles.title}>{task.title}</span>
                    <span className={styles.badgeLight}>{task.status}</span>
                    {isOverdue(task) && <span className={styles.badgeDanger}>Vencida</span>}
                  </div>
                  <p className={styles.meta}>{task.description || 'Sin descripción'}</p>
                  <div className={styles.metaRow}>
                    <span>Prioridad: {task.priority}</span>
                    <span>Proyecto: {task.projectId}</span>
                  </div>
                </div>
              ))
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
