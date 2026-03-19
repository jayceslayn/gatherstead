export function useAuth() {
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      loggedIn: ref(true),
      user: ref({ name: 'Demo User', email: 'demo@example.com' }),
      login: () => navigateTo('/tenants'),
      logout: () => navigateTo('/'),
    }
  }

  const { loggedIn, user } = useUserSession()

  return {
    loggedIn,
    user,
    login: () => navigateTo('/auth/azure', { external: true }),
    logout: () => navigateTo('/auth/logout', { external: true }),
  }
}
