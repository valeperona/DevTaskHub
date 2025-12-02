import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiClient } from '../api/apiClient';
import type { Project, ProjectMember, TaskItem, TaskPriority, TaskStatus } from '../api/apiClient';
import styles from './ProjectBoardPage.module.css';

const statusColumns: TaskStatus[] = ['ToDo', 'InProgress', 'InReview', 'Done'];

const isOverdue = (task: TaskItem) => {
  if (!task.dueDate) return false;
  const due = new Date(task.dueDate).getTime();
  return task.status !== 'Done' && due < Date.now();
};

export function ProjectBoardPage() {
  const { projectId } = useParams();
  const [project, setProject] = useState<Project | null>(null);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [newTask, setNewTask] = useState({
    title: '',
    description: '',
    priority: 'Medium' as TaskPriority,
    assignedToUserId: '',
    dueDate: '',
    labels: ''
  });
  const [filters, setFilters] = useState({ assignee: 'all', priority: 'all' as 'all' | TaskPriority });
  const [draggingTaskId, setDraggingTaskId] = useState<string | null>(null);
  const [newMember, setNewMember] = useState({ email: '', role: 'Collaborator' as 'Collaborator' | 'Owner' | 'Viewer' });
  const [formError, setFormError] = useState<string | null>(null);
  const session = useMemo(() => {
    const stored = localStorage.getItem('devtaskhub:session');
    return stored ? (JSON.parse(stored) as { userId: string }) : null;
  }, []);

  const loadProject = async () => {
    if (!projectId) return;
    setLoading(true);
    try {
      const data = await apiClient.getProject(projectId);
      setProject(data);
      setTasks(data.tasks ?? []);
      setMembers(data.members ?? []);
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

  const userRole = useMemo(() => {
    if (!project || !session?.userId) return null;
    if (project.ownerId === session.userId) return 'Owner';
    const member = project.members?.find((m) => m.userId === session.userId);
    return member?.role ?? null;
  }, [project, session]);

  const isViewer = userRole === 'Viewer';
  const isCollaborator = userRole === 'Collaborator';
  const isOwner = userRole === 'Owner';

  const groupedTasks = useMemo(() => {
    const filtered = tasks.filter((task) => {
      const assigneeOk = filters.assignee === 'all' || task.assignedToUserId === filters.assignee;
      const priorityOk = filters.priority === 'all' || task.priority === filters.priority;
      return assigneeOk && priorityOk;
    });
    return statusColumns.map((status) => ({
      status,
      items: filtered.filter((task) => task.status === status)
    }));
  }, [tasks, filters]);

  const handleCreateTask = async (event: FormEvent) => {
    event.preventDefault();
    if (isViewer) {
      setFormError('No tenés permisos para crear tareas');
      return;
    }
    if (!projectId || !newTask.title.trim()) {
      setFormError('El título de la tarea es obligatorio');
      return;
    }

    try {
      await apiClient.addTaskToProject(projectId, {
        title: newTask.title.trim(),
        description: newTask.description.trim() || undefined,
        priority: newTask.priority,
        assignedToUserId: newTask.assignedToUserId || undefined,
        dueDate: newTask.dueDate ? newTask.dueDate : undefined,
        labels: newTask.labels ? newTask.labels.split(',').map((l) => l.trim()).filter(Boolean) : []
      });
      setNewTask({ title: '', description: '', priority: 'Medium', assignedToUserId: '', dueDate: '', labels: '' });
      setError(null);
      setFormError(null);
      await loadProject();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'No se pudo crear la tarea');
    }
  };

  const handleAddMember = async (event: FormEvent) => {
    event.preventDefault();
    if (isViewer || isCollaborator) {
      setError('Solo el owner puede invitar miembros');
      return;
    }
    if (!projectId || !newMember.email.trim()) {
      setError('El email del nuevo miembro es obligatorio');
      return;
    }
    try {
      await apiClient.inviteMember(projectId, { email: newMember.email.trim().toLowerCase(), role: newMember.role });
      setNewMember({ email: '', role: 'Collaborator' });
      setError(null);
      setInfo('Invitación enviada. Espera que el usuario la acepte en su bandeja de invitaciones.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo agregar el miembro');
      setInfo(null);
    }
  };

  const handleTaskUpdate = async (
    taskId: string,
    status: TaskStatus,
    priority: TaskPriority,
    assignedToUserId?: string | null,
    dueDate?: string | null,
    labels?: string[]
  ) => {
    try {
      await apiClient.updateTask(taskId, { status, priority, assignedToUserId, dueDate, labels });
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
          {userRole && <p className={styles.roleHint}>Tu rol: {userRole}</p>}
        </div>
        <div className={styles.filters}>
          <label>
            Asignada a:
            <select
              value={filters.assignee}
              onChange={(e) => setFilters((prev) => ({ ...prev, assignee: e.target.value }))}
              data-cy="filter-assignee"
            >
              <option value="all">Todos</option>
              {members.map((member) => (
                <option key={member.userId} value={member.userId}>
                  {member.user?.email ?? (member as any).email ?? member.userId}
                </option>
              ))}
            </select>
          </label>
          <label>
            Prioridad:
            <select
              value={filters.priority}
              onChange={(e) => setFilters((prev) => ({ ...prev, priority: e.target.value as TaskPriority | 'all' }))}
              data-cy="filter-priority"
            >
              <option value="all">Todas</option>
              <option value="Low">Baja</option>
              <option value="Medium">Media</option>
              <option value="High">Alta</option>
            </select>
          </label>
        </div>
        <form className={styles.addMember} onSubmit={handleAddMember}>
          <input
            type="email"
            placeholder="Email colaborador"
            value={newMember.email}
            onChange={(e) => setNewMember((prev) => ({ ...prev, email: e.target.value }))}
            data-cy="member-email-input"
          />
          <select
            value={newMember.role}
            onChange={(e) => setNewMember((prev) => ({ ...prev, role: e.target.value as 'Collaborator' | 'Owner' | 'Viewer' }))}
            data-cy="member-role-select"
          >
            <option value="Collaborator">Colaborador</option>
            <option value="Viewer">Viewer</option>
          </select>
          <button type="submit" data-cy="member-add-button" disabled={!isOwner}>
            Añadir miembro
          </button>
          {userRole && <span className={styles.roleHint}>Tu rol: {userRole}</span>}
        </form>
        {info && <div className={styles.info}>{info}</div>}
      </div>

      <div className={styles.newTaskCard} data-cy="new-task-card">
        <form onSubmit={handleCreateTask} data-cy="new-task-form">
          {formError && <p className={styles.error}>{formError}</p>}
          <input
            type="text"
            placeholder="Título de la tarea"
            value={newTask.title}
            onChange={(event) => setNewTask((prev) => ({ ...prev, title: event.target.value }))}
            data-cy="task-title-input"
          />
          <textarea
            placeholder="Descripción"
            value={newTask.description}
            onChange={(event) => setNewTask((prev) => ({ ...prev, description: event.target.value }))}
            data-cy="task-desc-input"
          />
          <div className={styles.inlineInputs}>
            <select
              value={newTask.priority}
              onChange={(event) => setNewTask((prev) => ({ ...prev, priority: event.target.value as TaskPriority }))}
              data-cy="task-priority-select"
            >
              <option value="Low">Baja</option>
              <option value="Medium">Media</option>
              <option value="High">Alta</option>
            </select>
            <select
              value={newTask.assignedToUserId}
              onChange={(event) => setNewTask((prev) => ({ ...prev, assignedToUserId: event.target.value }))}
              data-cy="task-assignee-select"
            >
              <option value="">Sin asignar</option>
              {members.map((member) => (
                <option key={member.userId} value={member.userId}>
                  {member.user?.email ?? member.userId}
                </option>
              ))}
            </select>
            <input
              type="date"
              value={newTask.dueDate}
              onChange={(event) => setNewTask((prev) => ({ ...prev, dueDate: event.target.value }))}
              data-cy="task-due-date"
            />
          </div>
          <input
            type="text"
            placeholder="Etiquetas (separadas por coma)"
            value={newTask.labels}
            onChange={(event) => setNewTask((prev) => ({ ...prev, labels: event.target.value }))}
            data-cy="task-labels-input"
          />
          <button type="submit" className={styles.addTask} data-cy="task-save-button">
            Agregar tarea
          </button>
        </form>
      </div>

      {error && (
        <div className={styles.alert} data-cy="error-message">
          <strong>Algo salió mal:</strong> {error}
        </div>
      )}

      <div className={styles.columns}>
        {groupedTasks.map((column) => (
          <div
            key={column.status}
            className={styles.column}
            data-cy={`column-${column.status}`}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              if (draggingTaskId && !isViewer) {
                const dragged = tasks.find((t) => t.id === draggingTaskId);
                handleTaskUpdate(
                  draggingTaskId,
                  column.status,
                  dragged?.priority ?? 'Medium',
                  dragged?.assignedToUserId,
                  dragged?.dueDate ?? null,
                  dragged?.labels ?? []
                );
                setDraggingTaskId(null);
              }
            }}
          >
            <h3 className={styles.columnTitle}>{column.status}</h3>
            {column.items.length === 0 && <p className={styles.taskMeta}>Sin tareas</p>}
            {column.items.map((task) => (
              <div
                key={task.id}
                className={`${styles.taskCard} ${isOverdue(task) ? styles.overdue : ''}`}
                data-cy="task-card"
                draggable
                onDragStart={() => setDraggingTaskId(task.id)}
              >
                <h4 className={styles.taskTitle}>{task.title}</h4>
                <p>{task.description || 'Sin descripción'}</p>
                <div className={styles.taskMeta}>
                  <span>Prioridad: {task.priority}</span>
                  <span>{new Date(task.createdAt).toLocaleDateString()}</span>
                  {task.dueDate && <span>Vence: {new Date(task.dueDate).toLocaleDateString()}</span>}
                  {task.assignedTo?.email && <span>Asignada a: {task.assignedTo.email}</span>}
                  {task.labels?.length ? <span>Etiquetas: {task.labels.join(', ')}</span> : null}
                  {isOverdue(task) && <span className={styles.badgeDanger}>Vencida</span>}
                  {task.completedLate && <span className={styles.badgeWarning}>Cerrada tarde</span>}
                </div>
                <div className={styles.taskControls}>
                  <select
                    value={task.status}
                    onChange={(event) =>
                      handleTaskUpdate(task.id, event.target.value as TaskStatus, task.priority, task.assignedToUserId, task.dueDate, task.labels)
                    }
                    data-cy="task-status-select"
                    disabled={isViewer}
                  >
                    {statusColumns.map((status) => (
                      <option value={status} key={status}>
                        {status}
                      </option>
                    ))}
                  </select>
                  <select
                    value={task.priority}
                    onChange={(event) =>
                      handleTaskUpdate(task.id, task.status, event.target.value as TaskPriority, task.assignedToUserId, task.dueDate, task.labels)
                    }
                    data-cy="task-priority-select-existing"
                    disabled={isViewer}
                  >
                    <option value="Low">Baja</option>
                    <option value="Medium">Media</option>
                    <option value="High">Alta</option>
                  </select>
                  <select
                value={task.assignedToUserId ?? ''}
                onChange={(event) =>
                  handleTaskUpdate(
                    task.id,
                    task.status,
                    task.priority,
                    event.target.value || null,
                    task.dueDate,
                    task.labels
                  )
                }
                data-cy="task-assignee-select-existing"
                disabled={isViewer}
              >
                <option value="">Sin asignar</option>
                {members.map((member) => (
                  <option key={member.userId} value={member.userId}>
                    {member.user?.email ?? (member as any).email ?? member.userId}
                  </option>
                ))}
              </select>
                  <button
                    type="button"
                    className={styles.deleteTask}
                    onClick={() => handleDeleteTask(task.id)}
                    data-cy="delete-task-button"
                    disabled={isViewer}
                  >
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
