<script setup lang="ts">
const { locale, locales, setLocale } = useI18n()

const items = computed(() =>
  locales.value.map(l => ({
    label: l.name ?? l.code,
    onSelect: () => setLocale(l.code),
  }))
)
</script>

<template>
  <UDropdownMenu :items="[items]">
    <UButton variant="ghost" icon="i-heroicons-language" :label="locale.toUpperCase()" />
  </UDropdownMenu>
</template>
