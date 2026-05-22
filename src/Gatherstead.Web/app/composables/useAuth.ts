import { DEMO_USER, DEMO_USER_DISPLAY_NAME } from '~/repositories/demo/DemoStore'

export function useAuth() {
  const config = useRuntimeConfig()

  if (config.public.demoMode) {
    return {
      loggedIn: ref(true),
      user: ref({ name: DEMO_USER_DISPLAY_NAME, email: DEMO_USER.externalId }),
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
