import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { vi } from 'vitest';
import { ProjectBoardPage } from '../pages/ProjectBoardPage';

vi.mock('../api/apiClient', () => ({
  apiClient: {
    getProject: vi.fn(),
    addTaskToProject: vi.fn(),
    updateTask: vi.fn(),
    deleteTask: vi.fn(),
    inviteMember: vi.fn()
  }
}));

const mockedApi = require('../api/apiClient').apiClient as {
  getProject: ReturnType<typeof vi.fn>;
  addTaskToProject: ReturnType<typeof vi.fn>;
  updateTask: ReturnType<typeof vi.fn>;
};

const renderWithRoute = (route: string) =>
  render(
    <MemoryRouter initialEntries={[route]}>
      <Routes>
        <Route path="/projects/:projectId" element={<ProjectBoardPage />} />
      </Routes>
    </MemoryRouter>
  );

describe('ProjectBoardPage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('muestra tareas por columnas y filtra por asignado', async () => {
    localStorage.setItem('devtaskhub:session', JSON.stringify({ userId: 'owner1', email: 'o@test.com', token: 'x' }));
    mockedApi.getProject.mockResolvedValue({
      id: 'p1',
      name: 'Project',
      description: '',
      ownerId: 'owner1',
      members: [
        { userId: 'owner1', role: 'Owner', user: { id: 'owner1', email: 'o@test.com' } },
        { userId: 'collab1', role: 'Collaborator', user: { id: 'collab1', email: 'c@test.com' } }
      ],
      tasks: [
        {
          id: 't1',
          title: 'Tarea 1',
          description: '',
          status: 'ToDo',
          priority: 'Medium',
          projectId: 'p1',
          assignedToUserId: 'collab1',
          assignedTo: { id: 'collab1', email: 'c@test.com' },
          createdAt: new Date().toISOString(),
          labels: []
        }
      ]
    });

    renderWithRoute('/projects/p1');

    expect(await screen.findByText('Tarea 1')).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText(/Asignada a/i), 'owner1');

    await waitFor(() => expect(screen.getByText(/Sin tareas/i)).toBeInTheDocument());
  });

  it('impide crear tareas si el usuario es viewer', async () => {
    localStorage.setItem('devtaskhub:session', JSON.stringify({ userId: 'viewer1', email: 'v@test.com', token: 'x' }));
    mockedApi.getProject.mockResolvedValue({
      id: 'p2',
      name: 'Project',
      description: '',
      ownerId: 'owner1',
      members: [{ userId: 'viewer1', role: 'Viewer', user: { id: 'viewer1', email: 'v@test.com' } }],
      tasks: []
    });

    renderWithRoute('/projects/p2');

    await screen.findByText('Project');
    await userEvent.type(screen.getByPlaceholderText(/Título de la tarea/i), 'Nueva tarea');
    fireEvent.click(screen.getByRole('button', { name: /Agregar tarea/i }));

    expect(await screen.findByText(/No tenés permisos para crear tareas/i)).toBeInTheDocument();
    expect(mockedApi.addTaskToProject).not.toHaveBeenCalled();
  });
});
