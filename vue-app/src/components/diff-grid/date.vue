<template>
    <sticky-rail
        :slide-limit="value.slideLimit"
        :padding="8"
        class="text-xs absolute text-gray-500 italic"
        :style="value.style"
    >
        {{ formatted }}
    </sticky-rail>
</template>

<script setup>
import moment from 'moment';
import { computed } from 'vue';
import StickyRail from '../sticky-rail.vue';

const props = defineProps({
    value: {
        type: Object,
        required: true,
    },
})

const formatted = computed(() => {
    return moment(props.value.date).calendar(null, {
        sameDay: '[Today]',
        lastDay: 'ddd',
        lastWeek: 'ddd',
        sameElse: function (now) {
            return this.isSame(now, 'year') ? 'MM/DD' : 'MM/DD/YY'
        },
    })
})
</script>
