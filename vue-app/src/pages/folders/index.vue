<template>
    <!-- TODO: Implement service start/stop feature -->

    <button class="font-bold border rounded" @click="newFolder">
        Add folder
    </button>

    <div v-for="folder in folders" :key="folder.Id">
        <router-link
            :to="{ name: '/folders/[id]', params: { id: folder.Id } }"
            class="border rounded"
        >
            {{ folder.Name }}
        </router-link>
    </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '@/util/helpers'

const folders = ref([])

onMounted(async () => {
    getFolders()
})

async function newFolder() {
    const picked = await get('folders/pick')

    if (picked.cancelled) return

    await post('folders', { name: picked, path: picked })

    getFolders()
}

async function getFolders() {
    folders.value = await get('folders')
}
</script>
