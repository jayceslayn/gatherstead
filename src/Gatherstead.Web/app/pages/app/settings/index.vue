<script setup lang="ts">
definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
</script>

<template>
  <div>
    <GsPageHeader :title="t('settings.title')" />

    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <NuxtLink to="/app/settings/account">
        <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer h-full">
          <div class="flex items-center gap-3">
            <div class="rounded-full bg-(--ui-bg-elevated) p-2.5 shrink-0">
              <UIcon name="i-heroicons-user-circle" class="size-6 text-primary" />
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-semibold truncate">{{ t('settings.account') }}</p>
              <p class="text-sm text-muted truncate">{{ t('account.subtitle') }}</p>
            </div>
            <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
          </div>
        </UCard>
      </NuxtLink>

      <NuxtLink to="/app/settings/users">
        <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer h-full">
          <div class="flex items-center gap-3">
            <div class="rounded-full bg-(--ui-bg-elevated) p-2.5 shrink-0">
              <UIcon name="i-heroicons-users" class="size-6 text-primary" />
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-semibold truncate">{{ t('settings.users') }}</p>
              <p class="text-sm text-muted truncate">{{ t('tenantUser.listTitle') }}</p>
            </div>
            <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
          </div>
        </UCard>
      </NuxtLink>
    </div>
  </div>
</template>
