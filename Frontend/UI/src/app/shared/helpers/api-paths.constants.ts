export const API_PATHS = {
  auth: {
    register: '/auth/register',
    login: '/auth/login',
    refresh: '/auth/refresh',
    logout: '/auth/logout',
  },
  tasks: {
    base: '/tasks',
    byId: (id: string) => `/tasks/${id}`,
    suggestions: '/tasks/suggestions',
    suggestionsCreate: '/tasks/suggestions/create',
  },
} as const;
