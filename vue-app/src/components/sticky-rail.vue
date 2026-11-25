<!-- TODO: See about reducing choppiness -->
<template>
    <div ref="containerEl" class="relative">
        <div
            ref="contentEl"
            class="absolute max-w-full"
            :style="{ left: `${left}px` }"
        >
            <slot />
        </div>
    </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'

const props = defineProps({
    slideLimit: {
        type: Number,
        default: -1,
    },
    padding: {
        type: Number,
        default: 0,
    },
})

const containerEl = ref(null)
const contentEl = ref(null)
const left = ref(0)

onMounted(() => {
    updatePosition()
    window.addEventListener('scroll', updatePosition, { capture: true, passive: true })
    window.addEventListener('resize', updatePosition, { passive: true })
})

onBeforeUnmount(() => {
    window.removeEventListener('scroll', updatePosition, { capture: true })
    window.removeEventListener('resize', updatePosition)
})

function updatePosition() {
    if (!containerEl.value) return

    left.value = getLeft()
}

function getLeft() {
    const rectLeft = containerEl.value.getBoundingClientRect().left
    const leftOverflow = -rectLeft + props.padding

    if (leftOverflow <= 0) return 0

    const containerWidth = containerEl.value.getBoundingClientRect().width
    const contentWidth = contentEl.value.getBoundingClientRect().width
    const rightBound = Math.min(Math.max(0, containerWidth - contentWidth), props.slideLimit)

    if (rightBound <= leftOverflow) return rightBound

    return leftOverflow
}
</script>