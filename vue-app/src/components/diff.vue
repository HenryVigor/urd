<template>
    <button :class="head ? 'font-bold' : ''" @click="rollBack(diff)">
        {{ diff.Id }}, {{ diff.ParentId }}
    </button>
</template>

<script setup>
import { post } from '@/util/helpers'

defineProps({
    diff: {
        type: Object,
        required: true,
    },
    head: Boolean,
})

async function rollBack(diff) {
    await post('files/roll-back', { diffId: diff.Id })
}
</script>
