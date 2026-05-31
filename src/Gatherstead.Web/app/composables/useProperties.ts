import { useTenantStore } from '~/stores/tenant'
import type { PropertySummary } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'


export function useProperties() {
  const tenantStore = useTenantStore()
  const { properties: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<PropertySummary[]>(
    () => `properties-${tenantStore.currentTenantId}`,
    () => repo.listProperties(tenantStore.currentTenantId!),
    { watch: [() => tenantStore.currentTenantId] },
  )

  return { properties: computed(() => data.value ?? []), pending, error, refresh }
}

export function useProperty(propertyId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { properties: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<PropertySummary | null>(
    () => `property-${tenantStore.currentTenantId}-${propertyId.value}`,
    () => repo.getProperty(tenantStore.currentTenantId!, propertyId.value),
    { watch: [propertyId] },
  )

  return { property: computed(() => data.value ?? null), pending, error, refresh }
}

export function usePropertyActions(refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { properties: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createProperty(name: string): Promise<boolean> {
    updating.value.push('new')
    try {
      await repo.createProperty(tenantStore.currentTenantId!, name)
      await refresh()
      return true
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return false
      }
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== 'new')
    }
  }

  async function updateProperty(propertyId: string, name: string): Promise<boolean> {
    updating.value.push(propertyId)
    try {
      await repo.updateProperty(tenantStore.currentTenantId!, propertyId, name)
      await refresh()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== propertyId)
    }
  }

  async function deleteProperty(propertyId: string) {
    updating.value.push(propertyId)
    try {
      await repo.deleteProperty(tenantStore.currentTenantId!, propertyId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== propertyId)
    }
  }

  return { updating, createProperty, updateProperty, deleteProperty }
}
