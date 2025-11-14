import { vi } from 'vitest';
import { apiClient } from './apiClient';

const mockFetch = () => vi.spyOn(globalThis, 'fetch');

describe('apiClient', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('getProjects_ReturnsData_WhenApiRespondsOk', async () => {
    // Arrange: mock fetch to return a successful JSON payload
    const projects = [
      { id: '1', name: 'Alpha', description: null, createdAt: '2025-01-01', tasks: [] }
    ];
    const fetchSpy = mockFetch().mockResolvedValue(
      new Response(JSON.stringify(projects), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    );

    // Act: call the client wrapper
    const result = await apiClient.getProjects();

    // Assert: it should return the parsed body and call fetch with the expected URL
    expect(fetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/projects'), expect.any(Object));
    expect(result).toEqual(projects);
  });

  it('getProjects_ThrowsError_WhenApiReturnsFailure', async () => {
    // Arrange: mock fetch to simulate a server error response
    mockFetch().mockResolvedValue(
      new Response('Internal error', {
        status: 500,
        headers: { 'Content-Type': 'text/plain' }
      })
    );

    // Act + Assert: the API client should reject with the response text
    await expect(apiClient.getProjects()).rejects.toThrow(/Internal error/);
  });
});
