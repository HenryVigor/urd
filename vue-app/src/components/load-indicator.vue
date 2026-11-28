<template>
    <div class="text-gray-400 space-y-4 text-center">
        <font-awesome-icon :icon="icon" :flip="flip" class="text-5xl" />
        <div>{{ message }}</div>
    </div>
</template>

<script setup>
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'

defineProps({
    icon: {
        type: Object,
        required: true,
    },
    message: {
        type: String,
        default: '',
    },
    flip: Boolean,
})
</script>
