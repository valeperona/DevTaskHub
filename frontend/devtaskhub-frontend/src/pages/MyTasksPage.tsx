import { useEffect, useState } from 'react';
import { apiClient } from '../api/apiClient';
import type { Project, TaskItem, TaskPriority, TaskStatus } from '../api/apiClient';
import styles from './MyTasksPage.module.css';

const statusOrder: TaskStatus[] = ['ToDo', 'InProgress', 'InReview', 'Done'];

function isOverdue(task: TaskItem) {
  if (!task.dueDate) return false;
  const due = new Date(task.dueDate).getTime();
  return task.status !== 'Done' && due < Date.now();
}

export function MyTasksPage() {
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [projects, setProjects] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const [taskData, projectData] = await Promise.all([apiClient.getMyTasks(), apiClient.getProjects()]);
      const map: Record<string, string> = {};
      projectData.forEach((p: Project) => {
        map[p.id] = p.name;
      });
      setProjects(map);
      setTasks(taskData);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron cargar tus tareas');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const handleUpdate = async (taskId: string, status: TaskStatus, priority: TaskPriority) => {
    try {
      await apiClient.updateTask(taskId, { status, priority });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar la tarea');
    }
  };

  const grouped = statusOrder.map((status) => ({
    status,
    items: tasks.filter((t) => t.status === status).sort((a, b) => (isOverdue(b) ? 1 : 0) - (isOverdue(a) ? 1 : 0))
  }));

  if (loading) return <p className={styles.meta}>Cargando tus tareas...</p>;
  if (error) return <p className={styles.error}>{error}</p>;

  return (
    <div className={styles.wrapper}>
      <div className={styles.header}>
        <h2>Mis tareas asignadas</h2>
        <p className={styles.meta}>Agrupadas por estado</p>
      </div>
      <div className={styles.columns}>
        {grouped.map((col) => (
          <div key={col.status} className={styles.column}>
            <h3>{col.status}</h3>
            {col.items.length === 0 && <p className={styles.meta}>Sin tareas</p>}
            {col.items.map((task) => (
              <div
                key={task.id}
                className={`${styles.card} ${isOverdue(task) ? styles.overdue : ''} ${task.completedLate ? styles.completedLate : ''}`}
              >
                <div className={styles.cardHeader}>
                  <span className={styles.title}>{task.title}</span>
                  {isOverdue(task) && <span className={styles.badge}>Vencida</span>}
                  {task.completedLate && <span className={styles.badgeAlt}>Cerrada tarde</span>}
                </div>
                <p className={styles.meta}>{task.description || 'Sin descripción'}</p>
                <div className={styles.metaRow}>
                  <span>Vence: {task.dueDate ? new Date(task.dueDate).toLocaleDateString() : 'N/A'}</span>
                  <span>Proyecto: {projects[task.projectId] ?? task.projectId}</span>
                </div>
                <div className={styles.controls}>
                  <select value={task.status} onChange={(e) => handleUpdate(task.id, e.target.value as TaskStatus, task.priority)}>
                    {statusOrder.map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </select>
                  <select value={task.priority} onChange={(e) => handleUpdate(task.id, task.status, e.target.value as TaskPriority)}>
                    <option value="Low">Baja</option>
                    <option value="Medium">Media</option>
                    <option value="High">Alta</option>
                  </select>
                </div>
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
