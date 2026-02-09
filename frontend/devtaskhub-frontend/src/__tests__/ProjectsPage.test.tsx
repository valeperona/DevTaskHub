import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { ProjectsPage } from '../pages/ProjectsPage';

vi.mock('../api/apiClient', () => ({
  apiClient: {
    getProjects: vi.fn(),
    createProject: vi.fn(),
    deleteProject: vi.fn(),
    leaveProject: vi.fn()
  }
}));

const mockedApi = require('../api/apiClient').apiClient as {
  getProjects: ReturnType<typeof vi.fn>;
  createProject: ReturnType<typeof vi.fn>;
  deleteProject: ReturnType<typeof vi.fn>;
  leaveProject: ReturnType<typeof vi.fn>;
};

const session = { userId: 'owner-1', email: 'owner@test.com', token: 'x' };

describe('ProjectsPage', () => {
  beforeEach(() => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('carga y muestra proyectos', async () => {
    mockedApi.getProjects.mockResolvedValue([
      { id: 'p1', name: 'Proyecto 1', description: 'desc', createdAt: new Date().toISOString(), ownerId: session.userId, members: [], tasks: [] }
    ]);

    render(
      <MemoryRouter>
        <ProjectsPage session={session} />
      </MemoryRouter>
    );

    expect(await screen.findByText('Proyecto 1')).toBeInTheDocument();
  });

  it('crea proyecto y lo agrega a la lista', async () => {
    mockedApi.getProjects.mockResolvedValue([]);
    mockedApi.createProject.mockResolvedValue({
      id: 'new',
      name: 'Nuevo',
      description: '',
      createdAt: new Date().toISOString(),
      ownerId: session.userId,
      members: [],
      tasks: []
    });

    render(
      <MemoryRouter>
        <ProjectsPage session={session} />
      </MemoryRouter>
    );

    await userEvent.click(screen.getByRole('button', { name: /Nuevo proyecto/i }));
    await userEvent.type(screen.getByPlaceholderText(/Nombre del proyecto/i), 'Nuevo');
    fireEvent.submit(screen.getByRole('form'));

    expect(await screen.findByText('Nuevo')).toBeInTheDocument();
  });

  it('elimina proyecto al confirmar', async () => {
    mockedApi.getProjects.mockResolvedValue([
      { id: 'p1', name: 'Proyecto 1', description: 'desc', createdAt: new Date().toISOString(), ownerId: session.userId, members: [], tasks: [] }
    ]);
    mockedApi.deleteProject.mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <ProjectsPage session={session} />
      </MemoryRouter>
    );

    const deleteButton = await screen.findByRole('button', { name: /Eliminar/i });
    fireEvent.click(deleteButton);

    await waitFor(() => expect(screen.queryByText('Proyecto 1')).not.toBeInTheDocument());
  });
});
