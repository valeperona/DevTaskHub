export type TaskStatus = 'ToDo' | 'InProgress' | 'InReview' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High';
export type ProjectRole = 'Owner' | 'Collaborator' | 'Viewer';

export interface TaskItem {
  id: string;
  projectId: string;
  assignedToUserId?: string | null;
  title: string;
  description?: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  dueDate?: string | null;
  labels: string[];
  createdAt: string;
  assignedTo?: UserSummary | null;
  completedLate?: boolean;
}

export interface Project {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
  ownerId: string;
  members: ProjectMember[];
  tasks: TaskItem[];
}

export interface UserSummary {
  id: string;
  email: string;
}

export interface ProjectMember {
  projectId: string;
  userId: string;
  role: ProjectRole;
  user?: UserSummary;
}

export interface ProjectInvitation {
  id: string;
  projectId: string;
  projectName: string;
  userId: string;
  email: string;
  role: ProjectRole;
  status: 'Pending' | 'Accepted' | 'Declined';
  createdAt: string;
}

const baseURL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/$/, '');
let authToken: string | null = null;

export interface AuthResponse {
  userId: string;
  email: string;
  token: string;
}

export const setAuthToken = (token: string | null) => {
  authToken = token;
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseURL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(authToken ? { Authorization: `Bearer ${authToken}` } : {}),
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
  login: (payload: { email: string; password: string }) =>
    request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload)
    }),
  register: (payload: { email: string; password: string }) =>
    request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(payload)
    }),
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
  addTaskToProject: (
    projectId: string,
    payload: {
      title: string;
      description?: string;
      priority: TaskPriority;
      assignedToUserId?: string | null;
      dueDate?: string | null;
      labels?: string[];
    }
  ) =>
    request<TaskItem>(`/api/projects/${projectId}/tasks`, {
      method: 'POST',
      body: JSON.stringify(payload)
    }),
  updateTask: (
    taskId: string,
    payload: { status: TaskStatus; priority: TaskPriority; assignedToUserId?: string | null; dueDate?: string | null; labels?: string[] }
  ) =>
    request<void>(`/api/tasks/${taskId}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    }),
  deleteTask: (taskId: string) =>
    request<void>(`/api/tasks/${taskId}`, {
      method: 'DELETE'
    }),
  getProjectMembers: (projectId: string) => request<ProjectMember[]>(`/api/projects/${projectId}/members`),
  leaveProject: (projectId: string) =>
    request<void>(`/api/projects/${projectId}/leave`, {
      method: 'POST'
    }),
  addProjectMember: (projectId: string, payload: { email: string; role?: ProjectRole }) =>
    request<ProjectMember>(`/api/projects/${projectId}/members`, {
      method: 'POST',
      body: JSON.stringify(payload)
    }),
  getMyTasks: () => request<TaskItem[]>('/api/tasks/mine'),
  getCalendarTasks: (range?: { from?: string; to?: string }) => {
    const params = new URLSearchParams();
    if (range?.from) params.append('from', range.from);
    if (range?.to) params.append('to', range.to);
    const suffix = params.toString() ? `?${params.toString()}` : '';
    return request<TaskItem[]>(`/api/tasks/calendar${suffix}`);
  },
  inviteMember: (projectId: string, payload: { email: string; role?: ProjectRole }) =>
    request<ProjectInvitation>(`/api/projects/${projectId}/invitations`, {
      method: 'POST',
      body: JSON.stringify(payload)
    }),
  getProjectInvitations: (projectId: string) => request<ProjectInvitation[]>(`/api/projects/${projectId}/invitations`),
  getMyInvitations: () => request<ProjectInvitation[]>('/api/projects/invitations/mine'),
  respondInvitation: (invitationId: string, status: 'Accepted' | 'Declined') =>
    request<void>(`/api/projects/invitations/${invitationId}/respond`, {
      method: 'POST',
      body: JSON.stringify({ status })
    })
};
