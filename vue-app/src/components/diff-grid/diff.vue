<template>
    <button
        class="border-3 rounded-full bg-blue-300"
        :class="[head ? 'border-4' : '', diff.IsSnapshot ? 'bg-green-600' : '']"
        :style="{ width: `${size}px`, height: `${size}px` }"
        @click="rollBack(diff)"
    >
        <div class="text-xs absolute -top-4 text-gray-500 italic">{{ date }}</div>

        <template v-if="head">
            <font-awesome-icon
                v-if="diff.IsSnapshot"
                :icon="faCamera"
                class="text-lg"
            />

            <font-awesome-icon v-else :icon="faCircle" class="text-lg pt-1" />
        </template>

        <template v-else>
            <font-awesome-icon
                v-if="diff.IsSnapshot"
                :icon="faCameraRegular"
                class="text-lg"
            />

            <font-awesome-icon
                v-else
                :icon="faCircleRegular"
                class="text-lg pt-1"
            />
        </template>
    </button>
</template>

<script setup>
import { post } from '@/util/helpers'
import {
    faCamera as faCameraRegular,
    faCircle as faCircleRegular,
} from '@fortawesome/free-regular-svg-icons'
import { faCamera, faCircle } from '@fortawesome/free-solid-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import moment from 'moment'
import { computed } from 'vue'

const props = defineProps({
    diff: {
        type: Object,
        required: true,
    },
    size: {
        type: Number,
        required: true,
    },
    head: Boolean,
})

const date = computed(() => {
    return moment(props.diff.CreatedAt).calendar(null, {
        sameDay: 'ddd',
        lastDay: 'ddd',
        lastWeek: 'ddd',
        sameElse: function (now) {
            return this.isSame(now, 'year') ? 'MM/DD' : 'MM/DD/YY'
        },
    })
})

async function rollBack(diff) {
    await post('files/roll-back', { diffId: diff.Id })
}
</script>
