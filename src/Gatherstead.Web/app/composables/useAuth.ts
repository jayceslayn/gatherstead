import { DEMO_USER_DISPLAY_NAME, DEMO_USER_EXTERNAL_ID } from '~/repositories/demo/demoConstants'

export function useAuth() {
  if (__DEMO_MODE__) {
    return {
      loggedIn: ref(true),
      user: ref({ name: DEMO_USER_DISPLAY_NAME, email: DEMO_USER_EXTERNAL_ID }),
      login: () => navigateTo('/tenants'),
      logout: () => navigateTo('/'),
    }
  }

  const { loggedIn, user } = useUserSession()
  // External (full-page) navigations bypass the Nuxt router, so page:loading
  // hooks never fire — start the indicator manually for visible feedback.
  return {
    loggedIn,
    user,
    login: () => {
      useLoadingIndicator().start()
      return navigateTo('/auth/azure', { external: true })
    },
    logout: () => {
      useLoadingIndicator().start()
      return navigateTo('/auth/logout', { external: true })
    },
  }
}
