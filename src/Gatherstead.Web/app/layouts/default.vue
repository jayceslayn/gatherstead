<script setup lang="ts">
import { useTenantStore } from '~/stores/tenant'
import { useTenantRole } from '~/composables/useTenantRole'
import { useCurrentMemberStore } from '~/stores/member'

const { t } = useI18n()
const { logout, user } = useAuth()
const tenantStore = useTenantStore()
const { isManagerOrAbove, isMemberOrAbove } = useTenantRole()
const currentMemberStore = useCurrentMemberStore()

const primaryNavItems = computed(() => [[
  { label: t('nav.dashboard'), icon: 'i-heroicons-home', to: '/app' },
  { label: t('nav.directory'), icon: 'i-heroicons-user-group', to: '/app/directory' },
  { label: t('nav.properties'), icon: 'i-heroicons-building-office-2', to: '/app/properties' },
  { label: t('nav.accommodations'), icon: 'i-heroicons-home-modern', to: '/app/accommodations' },
  { label: t('nav.equipment'), icon: 'i-heroicons-wrench-screwdriver', to: '/app/equipment' },
  { label: t('nav.events'), icon: 'i-heroicons-calendar-days', to: '/app/events' },
  { label: t('nav.shopping'), icon: 'i-heroicons-shopping-bag', to: '/app/shopping' },
]])

// Reports are viewable by Member+ (aggregated dietary needs are allergy-safety info); Settings is
// Manager+ only. Build the group from whichever items the current role can see.
const managementNavItems = computed(() => {
  const items: Array<{ label: string; icon: string; to: string }> = []
  if (isMemberOrAbove.value) items.push({ label: t('nav.reports'), icon: 'i-heroicons-chart-bar', to: '/app/reports' })
  if (isManagerOrAbove.value) items.push({ label: t('nav.settings'), icon: 'i-heroicons-cog-6-tooth', to: '/app/settings' })
  return items.length ? [items] : []
})

const accountMenuItems = computed(() => {
  const profileItem = (currentMemberStore.linkedMemberId && currentMemberStore.linkedHouseholdId)
    ? [{
        label: t('nav.yourProfile'),
        icon: 'i-heroicons-user-circle',
        to: `/app/directory/${currentMemberStore.linkedHouseholdId}/${currentMemberStore.linkedMemberId}`,
      }]
    : []

  const groups: Array<Array<{ label: string; icon: string; to?: string; onSelect?: () => void }>> = [
    [
      ...profileItem,
      {
        label: t('nav.account'),
        icon: 'i-heroicons-cog-6-tooth',
        to: '/user/settings',
      },
      {
        label: t('nav.switchGroup'),
        icon: 'i-heroicons-arrow-path',
        to: '/tenants',
      },
      {
        label: t('nav.support'),
        icon: 'i-heroicons-lifebuoy',
        to: '/contact',
      },
    ],
  ]

  if (!__DEMO_MODE__) {
    groups.push([{
      label: t('common.signOut'),
      icon: 'i-heroicons-arrow-right-on-rectangle',
      onSelect: () => logout(),
    }])
  }

  return groups
})

const displayName = computed(() => {
  const name = (user.value as { name?: string } | null)?.name
  return name ?? t('common.appName')
})

const mobileNavItems = computed(() => [
  { label: t('nav.dashboard'), icon: 'i-heroicons-home', to: '/app' },
  { label: t('nav.directory'), icon: 'i-heroicons-user-group', to: '/app/directory' },
  { label: t('nav.properties'), icon: 'i-heroicons-building-office-2', to: '/app/properties' },
  { label: t('nav.events'), icon: 'i-heroicons-calendar-days', to: '/app/events' },
])

// Secondary destinations live behind a "More" sheet so the bottom bar stays uncrowded.
const mobileMoreItems = computed(() => {
  const groups: Array<Array<{ label: string; icon: string; to: string }>> = [[
    { label: t('nav.accommodations'), icon: 'i-heroicons-home-modern', to: '/app/accommodations' },
    { label: t('nav.equipment'), icon: 'i-heroicons-wrench-screwdriver', to: '/app/equipment' },
    { label: t('nav.shopping'), icon: 'i-heroicons-shopping-bag', to: '/app/shopping' },
  ]]
  const mgmt: Array<{ label: string; icon: string; to: string }> = []
  if (isMemberOrAbove.value) mgmt.push({ label: t('nav.reports'), icon: 'i-heroicons-chart-bar', to: '/app/reports' })
  if (isManagerOrAbove.value) mgmt.push({ label: t('nav.settings'), icon: 'i-heroicons-cog-6-tooth', to: '/app/settings' })
  if (mgmt.length) groups.push(mgmt)
  return groups
})

const route = useRoute()
function isActive(path: string) {
  if (path === '/app') return route.path === '/app'
  return route.path.startsWith(path)
}
const isMoreActive = computed(() =>
  route.path.startsWith('/app/accommodations')
  || route.path.startsWith('/app/equipment')
  || route.path.startsWith('/app/shopping')
  || route.path.startsWith('/app/reports')
  || route.path.startsWith('/app/settings'),
)
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <div class="flex flex-1 min-h-0">
      <!-- Desktop sidebar (md+) -->
      <aside class="hidden md:flex w-50 border-r border-(--ui-border) p-4 flex-col shrink-0">
        <NuxtLink to="/app" class="mb-1">
          <picture>
            <source media="(min-width: 640px)" srcset="/images/gatherstead_logo2_full.png">
            <img src="/images/gatherstead_logo2_small.png" :alt="t('common.appName')" class="h-12 w-auto">
          </picture>
        </NuxtLink>

        <p v-if="tenantStore.currentTenantName" class="text-xs text-muted mb-6 pl-1 truncate">
          {{ tenantStore.currentTenantName }}
        </p>

        <UNavigationMenu orientation="vertical" :items="primaryNavItems" highlight class="mb-2" />
        <UNavigationMenu v-if="managementNavItems.length" orientation="vertical" :items="managementNavItems" highlight class="mt-2" />

        <div class="mt-auto pt-4 border-t border-(--ui-border) flex flex-col align-start gap-2">
          <LocaleSwitcher />
          <UDropdownMenu :items="accountMenuItems" :ui="{ content: 'w-52' }" class="flex-1 min-w-0">
            <UUser
              as="button"
              :name="displayName"
              :avatar="{ alt: displayName }"
              class="w-full px-2 py-1 rounded-md hover:bg-(--ui-bg-elevated) transition-colors truncate"
            />
          </UDropdownMenu>
        </div>
      </aside>

      <!-- Main content -->
      <UMain class="flex-1 min-w-0 p-4 md:p-6 pb-24 md:pb-6">
        <!-- Mobile top bar -->
        <div class="md:hidden flex items-center justify-between mb-4">
          <div class="flex items-center gap-2">
            <img src="/images/gatherstead_logo2_small.png" :alt="t('common.appName')" class="h-8 w-auto">
            <span v-if="tenantStore.currentTenantName" class="text-sm font-medium truncate max-w-40">
              {{ tenantStore.currentTenantName }}
            </span>
          </div>
          <div class="flex items-center gap-1">
            <LocaleSwitcher />
            <UDropdownMenu :items="accountMenuItems" :ui="{ content: 'w-52' }">
              <UUser
                as="button"
                size="sm"
                :avatar="{ alt: displayName }"
                :aria-label="t('nav.yourProfile')"
              />
            </UDropdownMenu>
          </div>
        </div>

        <slot />
      </UMain>
    </div>

    <!-- Mobile bottom tab bar -->
    <nav class="md:hidden fixed bottom-0 left-0 right-0 z-50 border-t border-(--ui-border) bg-(--ui-bg) flex items-stretch h-16 safe-b">
      <NuxtLink
        v-for="item in mobileNavItems"
        :key="item.to"
        :to="item.to"
        class="flex-1 flex flex-col items-center justify-center gap-0.5 text-xs transition-colors"
        :class="isActive(item.to) ? 'text-primary' : 'text-muted hover:text-default'"
      >
        <UIcon :name="item.icon" class="size-6" />
        <span>{{ item.label }}</span>
      </NuxtLink>
      <UDropdownMenu
        v-if="mobileMoreItems.length"
        :items="mobileMoreItems"
        :content="{ side: 'top', align: 'end' }"
        :ui="{ content: 'w-44' }"
        class="flex-1"
      >
        <button
          type="button"
          class="w-full h-full flex flex-col items-center justify-center gap-0.5 text-xs transition-colors"
          :class="isMoreActive ? 'text-primary' : 'text-muted hover:text-default'"
          :aria-label="t('nav.more')"
        >
          <UIcon name="i-heroicons-ellipsis-horizontal-circle" class="size-6" />
          <span>{{ t('nav.more') }}</span>
        </button>
      </UDropdownMenu>
    </nav>
  </div>
</template>
