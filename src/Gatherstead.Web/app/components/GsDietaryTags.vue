<script setup lang="ts">
import type { DietaryCategory } from '~/repositories/types'
import { useDietaryTags } from '~/composables/useDietaryTags'

const props = defineProps<{
  slugs: string[]
}>()

const { tagsByCategory, tagBySlug } = useDietaryTags()

const CATEGORY_ORDER: DietaryCategory[] = ['Allergy', 'Restriction', 'Diet']

type BadgeColor = 'error' | 'warning' | 'success' | 'neutral' | 'primary' | 'secondary' | 'info'

const CATEGORY_COLOR: Record<DietaryCategory, BadgeColor> = {
  Allergy: 'error',
  Restriction: 'warning',
  Diet: 'success',
}

const CATEGORY_ICON: Record<DietaryCategory, string> = {
  Allergy: 'i-heroicons-exclamation-triangle',
  Restriction: 'i-heroicons-no-symbol',
  Diet: 'i-heroicons-check-circle',
}

const resolvedByCategory = computed(() => {
  const slugSet = new Set(props.slugs.map(s => s.toLowerCase()))
  return CATEGORY_ORDER.map(cat => ({
    cat,
    tags: (tagsByCategory.value[cat] ?? []).filter(t => slugSet.has(t.slug.toLowerCase())),
  })).filter(g => g.tags.length > 0)
})
</script>

<template>
  <div class="flex flex-wrap gap-1.5">
    <template v-for="group in resolvedByCategory" :key="group.cat">
      <UBadge
        v-for="tag in group.tags"
        :key="tag.slug"
        :color="CATEGORY_COLOR[group.cat]"
        variant="subtle"
        size="sm"
      >
        <UIcon :name="CATEGORY_ICON[group.cat]" class="mr-1 size-3" />
        {{ tag.displayName }}
      </UBadge>
    </template>
    <!-- Fall back to raw slug for any tag not found in the lookup -->
    <template v-for="slug in slugs" :key="`raw-${slug}`">
      <UBadge
        v-if="!tagBySlug.has(slug.toLowerCase())"
        color="neutral"
        variant="subtle"
        size="sm"
      >
        {{ slug }}
      </UBadge>
    </template>
  </div>
</template>
