<!-- TODO: Review this AI-assisted component -->
<!-- TODO: Consider having branch labels that have extra room on the right to stay on the left side of the screen when scrolling right -->
<template>
    <div ref="containerEl" class="overflow-hidden whitespace-nowrap">
        <span
            ref="textEl"
            class="inline-block"
            :class="{ 'branch-marquee': overflow > 0 }"
            :style="{
                '--marquee-distance': `-${overflow}px`,
                '--marquee-duration': `${duration}s`,
            }"
        >
            <!-- {{ text }} -->
            <slot />
        </span>
    </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useSlots, watch } from 'vue';

const props = defineProps({
    text: { type: String, required: true },
})

const containerEl = ref(null)
const textEl = ref(null)
const overflow = ref(0)

const duration = computed(() => Math.max(3, (overflow.value / 30) * 2))

function measure() {
    if (!containerEl.value || !textEl.value) return

    overflow.value = Math.max(
        0,
        Math.ceil(textEl.value.scrollWidth - containerEl.value.clientWidth),
    )
}

onMounted(measure)


const slots = useSlots()
watch(() => slots.default(), () => nextTick(measure))


let observer
onMounted(() => {
    observer = new ResizeObserver(measure)
    observer.observe(containerEl.value)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<style scoped>
.branch-marquee {
    animation: branch-marquee var(--marquee-duration) ease-in-out infinite alternate;
}

@keyframes branch-marquee {
    0%,
    15% { transform: translateX(0); }
    85%,
    100% { transform: translateX(var(--marquee-distance)); }
}
</style>
