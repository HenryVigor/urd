<template>
    <div>
        <button
            class="p-1 border rounded"
            :disabled="isLoading"
            @click="fetchHelloWorld"
        >
            {{ isLoading ? 'Fetching...' : 'Fetch data from backend server' }}
        </button>

        <div><span class="font-bold">Data:</span> {{ backendJson?.data }}</div>
    </div>
</template>

<script setup>
import { apiFetch } from '@/util/helpers'
import { ref } from 'vue'

const isLoading = ref(false)
const backendJson = ref(null)

async function fetchHelloWorld() {
    isLoading.value = true
    backendJson.value = await apiFetch('helloworld')
    isLoading.value = false
}
</script>
