export default defineNuxtRouteMiddleware((to) => {
  const config = useRuntimeConfig()
  if (config.public.demoMode) return

  const { loggedIn } = useAuth()
  if (!loggedIn.value) {
    return navigateTo('/')
  }
})
