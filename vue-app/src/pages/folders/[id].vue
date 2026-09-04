<template>
    <router-link to="/folders" class="font-bold border rounded"
        >Back to folder index</router-link
    >
    <button class="border rounded" @click="refresh">Refresh</button>

    <div>{{ folder }}</div>

    <div v-for="file in files" class="border rounded">
        <router-link :to="`/files/${file.Id}`">{{ file.Name }}</router-link>
    </div>
</template>

<script setup>
import { get } from '@/util/helpers'
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

const folder = ref(null)
const files = ref([])
const refreshing = ref(false)

onMounted(() => {
    refresh()
})

async function refresh() {
    if (refreshing.value) return

    refreshing.value = true

    try {
        const response = await get(`folders/${route.params.id}`)
        folder.value = response.folder
        files.value = response.files
    } finally {
        refreshing.value = false
    }
}
</script>
