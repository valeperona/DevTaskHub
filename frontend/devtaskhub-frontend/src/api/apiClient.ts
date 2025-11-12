export type TaskStatus = 'ToDo' | 'InProgress' | 'InReview' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High';

export interface TaskItem {
  id: string;
  projectId: string;
  title: string;
  description?: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  createdAt: string;
}

export interface Project {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
  tasks: TaskItem[];
}

const baseURL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/$/, '');

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseURL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || 'Error al comunicarse con la API');
  }

  if (response.status === 204) {
    return null as T;
  }

  return response.json() as Promise<T>;
}

export const apiClient = {
  getProjects: () => request<Project[]>('/api/projects'),
  createProject: (payload: { name: string; description?: string }) =>
    request<Project>('/api/projects', {
      method: 'POST',
      body: JSON.stringify(payload)
    }),
  deleteProject: (projectId: string) =>
    request<void>(`/api/projects/${projectId}`, { method: 'DELETE' }),
  getProject: (projectId: string) => request<Project>(`/api/projects/${projectId}`),
  getProjectTasks: (projectId: string) => request<TaskItem[]>(`/api/projects/${projectId}/tasks`),
  addTaskToProject: (projectId: string, payload: { title: string; description?: string; priority: TaskPriority }) =>
    request<TaskItem>(`/api/projects/${projectId}/tasks`, {
      method: 'POST',
      body: JSON.stringify(payload)
    }),
  updateTask: (taskId: string, payload: { status: TaskStatus; priority: TaskPriority }) =>
    request<void>(`/api/tasks/${taskId}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    }),
  deleteTask: (taskId: string) =>
    request<void>(`/api/tasks/${taskId}`, {
      method: 'DELETE'
    })
};
