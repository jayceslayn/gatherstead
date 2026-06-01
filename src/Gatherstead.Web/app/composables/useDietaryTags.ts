import type { DietaryCategory, DietaryTag } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export function useDietaryTags() {
  const { dietaryTags: repo } = useRepositories()

  const { data, pending } = useAsyncData<DietaryTag[]>(
    'dietary-tags',
    () => repo.listDietaryTags(),
  )

  const tags = computed(() => data.value ?? [])

  const tagBySlug = computed(() => new Map(tags.value.map(t => [t.slug, t])))

  const tagsByCategory = computed(() => {
    const groups: Record<DietaryCategory, DietaryTag[]> = { Diet: [], Allergy: [], Restriction: [] }
    for (const tag of tags.value) groups[tag.category].push(tag)
    return groups
  })

  function displayName(slug: string): string {
    return tagBySlug.value.get(slug)?.displayName ?? slug
  }

  function category(slug: string): DietaryCategory {
    return tagBySlug.value.get(slug)?.category ?? 'Diet'
  }

  return { tags, tagsByCategory, tagBySlug, displayName, category, pending }
}
