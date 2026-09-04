<template>
    <router-link
        :to="`/folders/${file?.FolderId}`"
        class="font-bold border rounded"
    >
        Back to folder
    </router-link>

    <div>{{ file }}</div>
</template>

<script setup>
import { get } from '@/util/helpers'
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

const file = ref(null)

onMounted(async () => {
    try {
        file.value = (await get(`files/${useRoute().params.id}`)).file
    } catch {}
})
</script>
