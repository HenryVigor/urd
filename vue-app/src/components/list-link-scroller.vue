<template>
    <recycle-scroller
        v-slot="{ item }"
        class="w-full flex-1 min-h-0 -my-2 pt-2 pb-1"
        :items="list"
        :item-size="44"
        key-field="Id"
    >
        <slot :item="item" />
    </recycle-scroller>
</template>

<script setup>
import { RecycleScroller } from 'vue-virtual-scroller'

defineProps({
    list: {
        type: Array,
        required: true,
    },
})
</script>
