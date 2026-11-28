<template>
    <sticky-rail
        :slide-limit="value.slideLimit"
        :padding="8"
        class="font-bold italic text-xs"
        :style="value.style"
    >
        <marquee>{{ value.name }}</marquee>
    </sticky-rail>
</template>

<script setup>
import Marquee from '@/components/marquee.vue';
import StickyRail from '@/components/sticky-rail.vue';

defineProps({
    value: {
        type: Object,
        required: true,
    },
})
</script>
