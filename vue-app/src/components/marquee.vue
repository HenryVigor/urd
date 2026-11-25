<!-- TODO: Review this AI-assisted component -->
<!-- TODO: Consider having branch labels that have extra room on the right stay on the left side of the screen when scrolling right -->
<template>
    <div ref="containerEl" class="overflow-hidden whitespace-nowrap">
        <span
            ref="contentEl"
            class="inline-block"
            :class="{ 'marquee': overflow > 0 }"
            :style="{
                '--marquee-distance': `-${overflow}px`,
                '--marquee-duration': `${duration}s`,
            }"
        >
            <slot />
        </span>
    </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

const containerEl = ref(null)
const contentEl = ref(null)
const overflow = ref(0)

const duration = computed(() => Math.max(3, (overflow.value / 30) * 2))

let observer

onMounted(() => {
    observer = new ResizeObserver(measure)
    observer.observe(containerEl.value)
    observer.observe(contentEl.value)
})

onBeforeUnmount(() => observer?.disconnect())

function measure() {
    if (!containerEl.value || !contentEl.value) return

    overflow.value = Math.max(
        0,
        Math.ceil(contentEl.value.scrollWidth - containerEl.value.clientWidth),
    )
}
</script>

<style scoped>
.marquee {
    animation: marquee var(--marquee-duration) ease-in-out infinite
        alternate;
}

@media (prefers-reduced-motion: reduce) {
    .marquee {
        animation: none;
    }
}

@keyframes marquee {
    0%,
    15% {
        transform: translateX(0);
    }
    85%,
    100% {
        transform: translateX(var(--marquee-distance));
    }
}
</style>
