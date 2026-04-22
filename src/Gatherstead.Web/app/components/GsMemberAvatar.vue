<script setup lang="ts">
const props = withDefaults(defineProps<{
  name: string
  size?: 'xs' | 'sm' | 'md' | 'lg'
}>(), {
  size: 'md',
})

const initials = computed(() => {
  const parts = props.name.trim().split(/\s+/)
  if (parts.length >= 2) {
    return ((parts[0]?.[0] ?? '') + (parts[parts.length - 1]?.[0] ?? '')).toUpperCase()
  }
  return props.name.substring(0, 2).toUpperCase()
})

const sizeClasses: Record<'xs' | 'sm' | 'md' | 'lg', string> = {
  xs: 'size-6 text-xs',
  sm: 'size-8 text-sm',
  md: 'size-10 text-base',
  lg: 'size-12 text-lg',
}
const sizeClass = computed(() => sizeClasses[props.size])
</script>

<template>
  <div
    class="flex items-center justify-center rounded-full bg-(--ui-primary)/10 text-primary font-semibold select-none shrink-0"
    :class="sizeClass"
    :title="name"
  >
    {{ initials }}
  </div>
</template>
