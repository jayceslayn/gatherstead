<script setup lang="ts">
import type { AgeBand, EventReportMeal, MealPlan } from '~/repositories/types'
import { buildAgeBandGroups } from '~/composables/useMealPlannerView'
import { useAgeBands } from '~/composables/useAgeBands'

// One meal on the planner: headline badges, the age-band × dietary pivot with a
// per-band attendee list, and the menu-notes form (editable for Coordinator+ or the
// meal's volunteer cook — the backend enforces the same gate).
const props = defineProps<{
  meal: EventReportMeal
  /** Authoritative plan record (notes + exception fields); the report copy may lag a save. */
  plan: MealPlan | null
  canEdit: boolean
  saving: boolean
}>()

const emit = defineEmits<{ saveNotes: [notes: string | null] }>()

const { t } = useI18n()
const { displayName } = useAgeBands()

const ageBandGroups = computed(() => buildAgeBandGroups(props.meal))

const bandLabel = (band: AgeBand | null) =>
  band ? displayName(band) : t('mealPlanner.unknownAge')

const savedNotes = computed(() => props.plan?.notes ?? props.meal.notes ?? null)

// Draft follows the saved value until the user edits; a refresh after save re-syncs it.
const notesDraft = ref(savedNotes.value ?? '')
watch(savedNotes, (value) => { notesDraft.value = value ?? '' })

const notesDirty = computed(() => notesDraft.value.trim() !== (savedNotes.value ?? ''))

function saveNotes() {
  const trimmed = notesDraft.value.trim()
  emit('saveNotes', trimmed.length ? trimmed : null)
}

// Progressive disclosure per band, matching the report's collapsed-by-default rows.
const expandedBands = ref<Set<string>>(new Set())
function toggleBand(key: string, open: boolean) {
  const next = new Set(expandedBands.value)
  if (open) next.add(key)
  else next.delete(key)
  expandedBands.value = next
}
</script>

<template>
  <UCard :ui="{ body: 'p-4 sm:p-4' }">
    <div class="flex items-center gap-2 flex-wrap">
      <p class="font-semibold">{{ t(`event.meal.${meal.mealType.toLowerCase()}`) }}</p>
      <span class="text-xs text-muted truncate">{{ meal.templateName }}</span>
    </div>

    <div class="flex flex-wrap gap-1.5 mt-2">
      <UBadge color="success" variant="subtle" icon="i-heroicons-check">
        {{ t('report.event.goingCount', { n: meal.going }) }}
      </UBadge>
      <UBadge v-if="meal.maybe" color="neutral" variant="subtle" icon="i-heroicons-question-mark-circle">
        {{ t('report.event.maybeCount', { n: meal.maybe }) }}
      </UBadge>
      <UBadge v-if="meal.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
        {{ t('report.event.bringingOwnFood', { n: meal.bringOwnFood }) }}
      </UBadge>
    </div>

    <!-- Age-band × dietary pivot -->
    <div v-if="ageBandGroups.length" class="mt-4 space-y-3">
      <p class="text-muted text-xs uppercase tracking-wide">{{ t('mealPlanner.byAgeGroup') }}</p>
      <div
        v-for="group in ageBandGroups"
        :key="group.band ?? 'unknown'"
        class="rounded-lg border border-(--ui-border) p-3"
      >
        <div class="flex items-center gap-2 flex-wrap">
          <p class="text-sm font-medium">{{ bandLabel(group.band) }}</p>
          <UBadge color="success" variant="subtle" size="sm">
            {{ t('report.event.goingCount', { n: group.going }) }}
          </UBadge>
          <UBadge v-if="group.maybe" color="neutral" variant="subtle" size="sm">
            {{ t('report.event.maybeCount', { n: group.maybe }) }}
          </UBadge>
          <UBadge v-if="group.bringOwnFood" color="neutral" variant="subtle" size="sm" icon="i-heroicons-shopping-bag">
            {{ t('report.event.bringingOwnFood', { n: group.bringOwnFood }) }}
          </UBadge>
        </div>

        <div class="flex flex-wrap gap-1.5 mt-2">
          <UBadge
            v-for="d in group.dietary"
            :key="d.label || '~none'"
            :color="d.label ? 'primary' : 'neutral'"
            variant="subtle"
            size="sm"
          >
            {{ t('report.event.dietaryTally', { label: d.label || t('mealPlanner.noRestrictions'), count: d.going + d.maybe }) }}
          </UBadge>
        </div>

        <GsCollapsible
          class="mt-2"
          button-class="text-xs text-muted"
          :open="expandedBands.has(group.band ?? 'unknown')"
          @update:open="toggleBand(group.band ?? 'unknown', $event)"
        >
          <span>{{ `${t('report.event.attendees')} (${group.attendees.length})` }}</span>
          <template #content>
            <ul class="mt-1.5 space-y-0.5 text-sm">
              <li v-for="att in group.attendees" :key="att.memberId" class="flex flex-col gap-0.5">
                <div class="flex items-center justify-between gap-2">
                  <span :class="att.status === 'Maybe' ? 'text-muted' : ''">{{ att.name }}</span>
                  <span class="flex items-center gap-1.5">
                    <span v-if="att.dietary.length" class="text-xs text-primary">{{ att.dietary.join(', ') }}</span>
                    <GsStatusBadge :status="att.status" icon-only />
                    <UBadge v-if="att.bringOwnFood" color="neutral" variant="subtle" icon="i-heroicons-shopping-bag">
                      {{ t('report.event.ownFood') }}
                    </UBadge>
                  </span>
                </div>
                <p v-if="att.dietaryNotes" class="text-xs text-muted italic pl-0.5">{{ att.dietaryNotes }}</p>
              </li>
            </ul>
          </template>
        </GsCollapsible>
      </div>
    </div>

    <!-- Menu notes -->
    <div class="mt-4">
      <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('mealPlanner.menuNotes') }}</p>
      <template v-if="canEdit">
        <UTextarea
          v-model="notesDraft"
          :rows="2"
          autoresize
          :maxlength="500"
          :placeholder="t('mealPlanner.notesPlaceholder')"
          class="w-full"
        />
        <div class="flex justify-end mt-2">
          <UButton
            size="sm"
            :disabled="!notesDirty"
            :loading="saving"
            @click="saveNotes"
          >
            {{ t('mealPlanner.saveNotes') }}
          </UButton>
        </div>
      </template>
      <p v-else-if="savedNotes" class="text-sm whitespace-pre-line">{{ savedNotes }}</p>
      <p v-else class="text-sm text-muted">{{ t('mealPlanner.noNotes') }}</p>
    </div>
  </UCard>
</template>
