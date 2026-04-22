<script setup lang="ts">
defineProps<{
  rows: { id: string; label: string }[]
  columns: { id: string; label: string }[]
}>()
</script>

<template>
  <!-- Desktop: scrollable table -->
  <div class="hidden sm:block overflow-x-auto">
    <table class="w-full text-sm">
      <thead>
        <tr>
          <th class="text-left py-2 pr-4 text-muted font-medium w-32 min-w-[8rem]"></th>
          <th
            v-for="col in columns"
            :key="col.id"
            class="py-2 px-3 text-muted font-medium text-center whitespace-nowrap"
          >
            {{ col.label }}
          </th>
        </tr>
      </thead>
      <tbody class="divide-y divide-(--ui-border)">
        <tr v-for="row in rows" :key="row.id">
          <td class="py-2 pr-4 text-xs text-muted whitespace-nowrap">{{ row.label }}</td>
          <td v-for="col in columns" :key="col.id" class="py-2 px-3 text-center">
            <slot name="cell" :row="row" :column="col" />
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- Mobile: days stacked vertically -->
  <div class="sm:hidden space-y-6">
    <div v-for="row in rows" :key="row.id">
      <p class="text-xs font-semibold text-muted uppercase tracking-wide mb-2">{{ row.label }}</p>
      <div class="space-y-2 pl-2">
        <div
          v-for="col in columns"
          :key="col.id"
          class="flex items-center justify-between gap-4 py-1"
        >
          <span class="text-sm">{{ col.label }}</span>
          <slot name="cell" :row="row" :column="col" />
        </div>
      </div>
    </div>
  </div>
</template>
