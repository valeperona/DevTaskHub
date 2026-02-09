import { apiClient, setAuthToken } from '../api/apiClient';

describe('apiClient', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    setAuthToken(null);
    global.fetch = vi.fn();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it('realiza login y retorna payload', async () => {
    const payload = { userId: '1', email: 'a@test.com', token: 'abc' };
    (global.fetch as vi.Mock).mockResolvedValue(
      new Response(JSON.stringify(payload), { status: 200, headers: { 'Content-Type': 'application/json' } })
    );

    const result = await apiClient.login({ email: 'a@test.com', password: '123456' });

    expect(result).toEqual(payload);
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/auth/login'),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('lanza error cuando la API responde no-OK', async () => {
    (global.fetch as vi.Mock).mockResolvedValue(new Response('fail', { status: 500 }));

    await expect(apiClient.getProjects()).rejects.toThrow('fail');
  });

  it('incluye Authorization cuando hay token', async () => {
    setAuthToken('jwt-token');
    (global.fetch as vi.Mock).mockResolvedValue(
      new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } })
    );

    await apiClient.getProjects();

    const [, init] = (global.fetch as vi.Mock).mock.calls[0];
    expect(init?.headers).toMatchObject({ Authorization: 'Bearer jwt-token' });
  });

  it('maneja respuestas 204 devolviendo null', async () => {
    (global.fetch as vi.Mock).mockResolvedValue(new Response(null, { status: 204 }));

    const result = await apiClient.deleteTask('123');

    expect(result).toBeNull();
  });
});
