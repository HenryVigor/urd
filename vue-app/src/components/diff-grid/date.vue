<template>
    <div class="text-xs absolute -top-4 text-gray-500 italic">
        {{ formatted }}
    </div>
</template>

<script setup>
import moment from 'moment';
import { computed } from 'vue';

const props = defineProps({
    date: {
        type: String,
        required: true,
    },
})

const formatted = computed(() => {
    return moment(props.date).calendar(null, {
        sameDay: '[Today]',
        lastDay: 'ddd',
        lastWeek: 'dddd',
        sameElse: function (now) {
            return this.isSame(now, 'year') ? 'MM/DD' : 'MM/DD/YY'
        },
    })
})
</script>
