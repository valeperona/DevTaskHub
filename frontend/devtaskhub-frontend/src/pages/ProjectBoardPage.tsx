import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiClient } from '../api/apiClient';
import type { Project, TaskItem, TaskPriority, TaskStatus } from '../api/apiClient';
import styles from './ProjectBoardPage.module.css';

const statusColumns: TaskStatus[] = ['ToDo', 'InProgress', 'InReview', 'Done'];

export function ProjectBoardPage() {
  const { projectId } = useParams();
  const [project, setProject] = useState<Project | null>(null);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [newTask, setNewTask] = useState({ title: '', description: '', priority: 'Medium' as TaskPriority });

  const loadProject = async () => {
    if (!projectId) return;
    setLoading(true);
    try {
      const data = await apiClient.getProject(projectId);
      setProject(data);
      setTasks(data.tasks ?? []);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar el proyecto');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProject();
  }, [projectId]);

  const groupedTasks = useMemo(() => {
    return statusColumns.map((status) => ({
      status,
      items: tasks.filter((task) => task.status === status)
    }));
  }, [tasks]);

  const handleCreateTask = async (event: FormEvent) => {
    event.preventDefault();
    if (!projectId || !newTask.title.trim()) {
      setError('El título de la tarea es obligatorio');
      return;
    }

    try {
      await apiClient.addTaskToProject(projectId, {
        title: newTask.title.trim(),
        description: newTask.description.trim() || undefined,
        priority: newTask.priority
      });
      setNewTask({ title: '', description: '', priority: 'Medium' });
      setError(null);
      await loadProject();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear la tarea');
    }
  };

  const handleTaskUpdate = async (taskId: string, status: TaskStatus, priority: TaskPriority) => {
    try {
      await apiClient.updateTask(taskId, { status, priority });
      setError(null);
      await loadProject();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar la tarea');
    }
  };

  const handleDeleteTask = async (taskId: string) => {
    if (!confirm('¿Eliminar esta tarea?')) return;
    try {
      await apiClient.deleteTask(taskId);
      setError(null);
      await loadProject();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar la tarea');
    }
  };

  if (loading) {
    return <p>Cargando tablero...</p>;
  }

  if (!projectId || !project) {
    return <p>No se encontró el proyecto solicitado.</p>;
  }

  return (
    <div className={styles.wrapper}>
      <div className={styles.meta}>
        <div>
          <Link to="/" className={styles.backLink}>
            ← Volver a proyectos
          </Link>
          <h2>{project.name}</h2>
          <p>{project.description || 'Sin descripción'}</p>
        </div>
      </div>

      <div className={styles.newTaskCard}>
        <form onSubmit={handleCreateTask}>
          <input
            type="text"
            placeholder="Título de la tarea"
            value={newTask.title}
            onChange={(event) => setNewTask((prev) => ({ ...prev, title: event.target.value }))}
          />
          <textarea
            placeholder="Descripción"
            value={newTask.description}
            onChange={(event) => setNewTask((prev) => ({ ...prev, description: event.target.value }))}
          />
          <select
            value={newTask.priority}
            onChange={(event) => setNewTask((prev) => ({ ...prev, priority: event.target.value as TaskPriority }))}
          >
            <option value="Low">Baja</option>
            <option value="Medium">Media</option>
            <option value="High">Alta</option>
          </select>
          <button type="submit" className={styles.addTask}>
            Agregar tarea
          </button>
        </form>
      </div>

      {error && <p className={styles.error}>{error}</p>}

      <div className={styles.columns}>
        {groupedTasks.map((column) => (
          <div key={column.status} className={styles.column}>
            <h3 className={styles.columnTitle}>{column.status}</h3>
            {column.items.length === 0 && <p className={styles.taskMeta}>Sin tareas</p>}
            {column.items.map((task) => (
              <div key={task.id} className={styles.taskCard}>
                <h4 className={styles.taskTitle}>{task.title}</h4>
                <p>{task.description || 'Sin descripción'}</p>
                <div className={styles.taskMeta}>
                  <span>Prioridad: {task.priority}</span>
                  <span>{new Date(task.createdAt).toLocaleDateString()}</span>
                </div>
                <div className={styles.taskControls}>
                  <select value={task.status} onChange={(event) => handleTaskUpdate(task.id, event.target.value as TaskStatus, task.priority)}>
                    {statusColumns.map((status) => (
                      <option value={status} key={status}>
                        {status}
                      </option>
                    ))}
                  </select>
                  <select value={task.priority} onChange={(event) => handleTaskUpdate(task.id, task.status, event.target.value as TaskPriority)}>
                    <option value="Low">Baja</option>
                    <option value="Medium">Media</option>
                    <option value="High">Alta</option>
                  </select>
                  <button type="button" className={styles.deleteTask} onClick={() => handleDeleteTask(task.id)}>
                    Eliminar
                  </button>
                </div>
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
