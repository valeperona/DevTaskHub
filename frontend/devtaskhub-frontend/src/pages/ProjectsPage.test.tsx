import { MemoryRouter } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import { ProjectsPage } from './ProjectsPage';
import { apiClient } from '../api/apiClient';

vi.mock('../api/apiClient', () => {
  const mockFns = {
    getProjects: vi.fn(),
    createProject: vi.fn(),
    deleteProject: vi.fn(),
    getProject: vi.fn(),
    getProjectTasks: vi.fn(),
    addTaskToProject: vi.fn(),
    updateTask: vi.fn()
  };
  return { apiClient: mockFns };
});

const mockedApiClient = vi.mocked(apiClient);

const renderPage = () =>
  render(
    <MemoryRouter>
      <ProjectsPage userEmail="tester@example.com" />
    </MemoryRouter>
  );

describe('ProjectsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows projects when api resolves successfully', async () => {
    // Arrange: mock API response
    mockedApiClient.getProjects.mockResolvedValue([
      {
        id: '1',
        name: 'Proyecto Alpha',
        description: 'Desc',
        createdAt: new Date().toISOString(),
        tasks: []
      }
    ]);

    // Act: render the component
    renderPage();

    // Assert: project name should appear after data load
    expect(await screen.findByText('Proyecto Alpha')).toBeInTheDocument();
    expect(mockedApiClient.getProjects).toHaveBeenCalledTimes(1);
  });

  it('shows error message when api call fails', async () => {
    // Arrange: make getProjects reject
    mockedApiClient.getProjects.mockRejectedValue(new Error('Fallo al cargar'));

    // Act: render the component
    renderPage();

    // Assert: component should surface the error text
    expect(await screen.findByText('Fallo al cargar')).toBeInTheDocument();
  });
});
