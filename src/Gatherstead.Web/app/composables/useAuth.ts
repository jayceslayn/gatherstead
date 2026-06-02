import { DEMO_USER_DISPLAY_NAME, DEMO_USER_EXTERNAL_ID } from '~/repositories/demo/demoConstants'

export function useAuth() {
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      loggedIn: ref(true),
      user: ref({ name: DEMO_USER_DISPLAY_NAME, email: DEMO_USER_EXTERNAL_ID }),
      login: () => navigateTo('/tenants'),
      logout: () => navigateTo('/'),
      isDemo: true,
    }
  }

  const { loggedIn, user } = useUserSession()

  return {
    loggedIn,
    user,
    login: () => navigateTo('/auth/azure', { external: true }),
    logout: () => navigateTo('/auth/logout', { external: true }),
    isDemo: false,
  }
}
