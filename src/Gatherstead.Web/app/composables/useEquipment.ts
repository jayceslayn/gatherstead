import { useTenantStore } from '~/stores/tenant'
import type { EquipmentSummary, AttributeWriteEntry } from '~/repositories/types'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

export function useEquipment() {
  const tenantStore = useTenantStore()
  const { equipment: repo } = useRepositories()

  const { data, pending, error, refresh } = useAsyncData<EquipmentSummary[]>(
    () => `equipment-${tenantStore.currentTenantId}`,
    () => repo.listEquipment(tenantStore.currentTenantId!),
    { watch: [() => tenantStore.currentTenantId] },
  )

  return { equipment: computed(() => data.value ?? []), pending, error, refresh }
}

export function useEquipmentActions(refresh: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { equipment: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function createEquipment(
    name: string,
    propertyId: string | null,
    notes: string | null,
    attributes: AttributeWriteEntry[],
  ): Promise<boolean> {
    updating.value.push('new')
    try {
      await repo.createEquipment(tenantStore.currentTenantId!, name, propertyId, notes, attributes)
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

  async function updateEquipment(
    equipmentId: string,
    name: string,
    propertyId: string | null,
    notes: string | null,
    attributes: AttributeWriteEntry[],
  ): Promise<boolean> {
    updating.value.push(equipmentId)
    try {
      await repo.updateEquipment(tenantStore.currentTenantId!, equipmentId, name, propertyId, notes, attributes)
      await refresh()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== equipmentId)
    }
  }

  async function deleteEquipment(equipmentId: string) {
    updating.value.push(equipmentId)
    try {
      await repo.deleteEquipment(tenantStore.currentTenantId!, equipmentId)
      await refresh()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== equipmentId)
    }
  }

  return { updating, createEquipment, updateEquipment, deleteEquipment }
}
