import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../api/apiClient';
import type { Project } from '../api/apiClient';
import styles from './ProjectsPage.module.css';

interface ProjectsPageProps {
  userEmail: string;
}

export function ProjectsPage({ userEmail }: ProjectsPageProps) {
  const navigate = useNavigate();
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [formValues, setFormValues] = useState({ name: '', description: '' });

  const loadProjects = async () => {
    setLoading(true);
    try {
      const data = await apiClient.getProjects();
      setProjects(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al cargar proyectos');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProjects();
  }, []);

  const handleCreateProject = async (event: FormEvent) => {
    event.preventDefault();
    if (!formValues.name.trim()) {
      setError('El nombre del proyecto es obligatorio');
      return;
    }

    try {
      const newProject = await apiClient.createProject({
        name: formValues.name.trim(),
        description: formValues.description.trim() || undefined
      });
      setProjects((prev) => [newProject, ...prev]);
      setFormValues({ name: '', description: '' });
      setShowForm(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el proyecto');
    }
  };

  const handleDeleteProject = async (projectId: string) => {
    if (!confirm('¿Eliminar este proyecto?')) return;
    try {
      await apiClient.deleteProject(projectId);
      setProjects((prev) => prev.filter((project) => project.id !== projectId));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar el proyecto');
    }
  };

  return (
    <div className={styles.wrapper}>
      <div className={styles.headerRow}>
        <div>
          <h2>Proyectos</h2>
          <p className={styles.projectMeta}>Sesión iniciada como {userEmail}</p>
        </div>
        <button className={styles.newProjectButton} onClick={() => setShowForm((prev) => !prev)}>
          {showForm ? 'Cancelar' : 'Nuevo proyecto'}
        </button>
      </div>

      {showForm && (
        <div className={styles.formCard}>
          <form onSubmit={handleCreateProject}>
            <input
              type="text"
              placeholder="Nombre del proyecto"
              value={formValues.name}
              onChange={(event) => setFormValues((prev) => ({ ...prev, name: event.target.value }))}
            />
            <textarea
              placeholder="Descripción breve"
              value={formValues.description}
              onChange={(event) => setFormValues((prev) => ({ ...prev, description: event.target.value }))}
            />
            <div>
              <button type="submit" className={styles.newProjectButton}>
                Crear proyecto
              </button>
            </div>
          </form>
        </div>
      )}

      {error && <p className={styles.projectMeta}>{error}</p>}

      {loading ? (
        <p className={styles.projectMeta}>Cargando proyectos...</p>
      ) : projects.length === 0 ? (
        <div className={styles.emptyState}>No hay proyectos todavía.</div>
      ) : (
        <div className={styles.projectList}>
          {projects.map((project) => (
            <div key={project.id} className={styles.projectCard} onClick={() => navigate(`/projects/${project.id}`)}>
              <h3>{project.name}</h3>
              <p className={styles.projectMeta}>{project.description || 'Sin descripción'}</p>
              <p className={styles.projectMeta}>
                {new Date(project.createdAt).toLocaleDateString()} • {project.tasks?.length ?? 0} tareas
              </p>
              <button
                className={styles.deleteButton}
                onClick={(event) => {
                  event.stopPropagation();
                  handleDeleteProject(project.id);
                }}
              >
                Eliminar
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
