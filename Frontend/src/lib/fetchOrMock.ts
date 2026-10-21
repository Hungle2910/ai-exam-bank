/**
 * Data loader with mock fallback for features whose backend endpoint is not built yet.
 *
 * Rules:
 * - Real API returns 2xx            -> real data, mock ignored.
 * - 404 / 501 / network error       -> mock data (endpoint not implemented yet),
 *                                      ONLY when mocks are enabled.
 * - Any other error (401/403/500…)  -> thrown, never hidden behind mock data.
 *
 * Mocks are enabled by default in development. Disable them with
 *   NEXT_PUBLIC_USE_MOCK=false
 * (always set this for staging/production builds).
 */
export const MOCK_ENABLED =
  process.env.NEXT_PUBLIC_USE_MOCK !== undefined
    ? process.env.NEXT_PUBLIC_USE_MOCK === 'true'
    : process.env.NODE_ENV !== 'production';

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

export async function fetchOrMock<T>(url: string, mock: T): Promise<{ data: T; isMock: boolean }> {
  let res: Response;
  try {
    res = await fetch(url, { credentials: 'include' });
  } catch {
    if (MOCK_ENABLED) return { data: mock, isMock: true };
    throw new ApiError(0, 'Không kết nối được máy chủ');
  }

  if (res.ok) return { data: (await res.json()) as T, isMock: false };

  if ((res.status === 404 || res.status === 501) && MOCK_ENABLED) {
    return { data: mock, isMock: true };
  }
  throw new ApiError(res.status, `API ${url} lỗi ${res.status}`);
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('vi-VN', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}
