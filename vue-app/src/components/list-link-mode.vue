<template>
    <div class="h-full flex gap-1">
        <icon-button
            :icon="faXmark"
            icon-class="text-xl text-red-400"
            @click="$emit('cancel')"
        />

        <slot />

        <icon-button
            :icon="faCheck"
            icon-class="text-xl text-green-700"
            @click="$emit('confirm')"
        />
    </div>
</template>

<script setup>
import { faCheck, faXmark } from '@fortawesome/free-solid-svg-icons'
import IconButton from '@/components/icon-button.vue'

defineEmits(['cancel', 'confirm'])
</script>
