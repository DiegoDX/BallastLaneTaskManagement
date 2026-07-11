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
    suggestionsGenerate: '/tasks/suggestions/generate',
    suggestionsCreate: '/tasks/suggestions/create',
  },
  chat: {
    base: '/chat',
  },
  taskAssistant: {
    base: '/task-assistant',
  },
  docAssistant: {
    base: '/doc-assistant',
  },
  agent: {
    base: '/agent',
    continue: '/agent/continue',
  },
} as const;
