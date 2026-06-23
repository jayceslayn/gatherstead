<script setup lang="ts">
import type { ShoppingScope } from '~/composables/useShoppingList'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const { events } = useEvents()
const { properties } = useProperties()

// Encoded selection: 'event:<id>' | 'property:<id>' | ''.
const selected = ref<string>(initialFromQuery())

function initialFromQuery(): string {
  if (typeof route.query.event === 'string') return `event:${route.query.event}`
  if (typeof route.query.property === 'string') return `property:${route.query.property}`
  return ''
}

const options = computed(() => [
  ...events.value.map(e => ({ label: `${t('shopping.origin.event')} · ${e.name}`, value: `event:${e.id}` })),
  ...properties.value.map(p => ({ label: `${t('shopping.origin.property')} · ${p.name}`, value: `property:${p.id}` })),
])

// Default to the first available scope once lists resolve, if none is chosen yet.
watch(options, (opts) => {
  if (!selected.value && opts.length) selected.value = opts[0]!.value
}, { immediate: true })

const scope = computed<ShoppingScope | null>(() => {
  const [kind, id] = selected.value.split(':')
  if (kind === 'event' && id) {
    const ev = events.value.find(e => e.id === id)
    return { kind: 'event', eventId: id, propertyId: ev?.propertyId ?? null }
  }
  if (kind === 'property' && id) return { kind: 'property', eventId: null, propertyId: id }
  return null
})

// Keep the URL query in sync so a selected list is shareable / bookmarkable.
watch(selected, (val) => {
  const [kind, id] = val.split(':')
  const query = kind === 'event' && id
    ? { event: id }
    : kind === 'property' && id
      ? { property: id }
      : {}
  void router.replace({ query })
})
</script>

<template>
  <div>
    <GsPageHeader :title="t('shopping.title')" />

    <div class="space-y-5">
      <UFormField :label="t('shopping.viewList')">
        <USelect
          v-model="selected"
          :items="options"
          :placeholder="t('shopping.selectScope')"
          icon="i-heroicons-shopping-bag"
          class="w-full max-w-md"
        />
      </UFormField>

      <GsShoppingList v-if="scope" :scope="scope" />
      <p v-else class="text-sm text-muted">{{ t('shopping.selectScope') }}</p>
    </div>
  </div>
</template>
