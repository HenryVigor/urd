<template>
    <layout no-padding>
        <template #header-left>
            <icon-button
                :to="{
                    path: `/folders/${file.Folder.Id}`,
                    query: { mode: file.Archived ? 'archived' : 'tracked' },
                    state: {
                        folder: {
                            Id: file.Folder.Id,
                            Name: file.Folder.Name,
                            Path: file.Folder.Path,
                            Archived: file.Folder.Archived,
                        },
                    },
                }"
                :icon="faCircleLeft"
                icon-class="text-2xl text-blue-800"
            />

            <div class="px-1">
                <div class="text-xl">{{ file.Name }}</div>
                <div class="text-xs text-gray-600">{{ systemName }}</div>
            </div>
        </template>

        <template #header-right>
            <font-awesome-icon
                v-if="file.Archived"
                :icon="faBoxOpen"
                class="text-3xl text-gray-600"
            />
        </template>

        <template v-if="!file.Diffs">
            <load-indicator
                v-if="longLoad"
                :icon="faFloppyDisk"
                message="Loading backups..."
                flip
                class="m-auto"
            />
        </template>

        <template v-else>
            <diff-grid
                :diffs="file.Diffs"
                :branches="file.Branches"
                :head-diff-id="file.HeadDiffId ?? null"
                class="p-2 pt-7"
            />
        </template>
    </layout>
</template>

<script setup>
import { faFloppyDisk } from '@fortawesome/free-regular-svg-icons'
import { faBoxOpen, faCircleLeft } from '@fortawesome/free-solid-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import DiffGrid from '@/components/diff-grid/diff-grid.vue'
import IconButton from '@/components/icon-button.vue'
import LoadIndicator from '@/components/load-indicator.vue'
import Layout from '@/layouts/layout.vue'
import { get } from '@/util/helpers'

const route = useRoute()

const file = ref(window.history.state.file)
const longLoad = ref(false)
const refreshing = ref(false)

let refreshTimer = null

onMounted(async () => {
    let loadIndicatorTimer

    try {
        loadIndicatorTimer = setTimeout(() => (longLoad.value = true), 300)

        await refresh()
    } finally {
        nextTick()
        clearTimeout(loadIndicatorTimer)
    }
})

onUnmounted(() => {
    clearTimeout(refreshTimer)
})

const systemName = computed(() => {
    return file.value.Path.split('/').pop()
})

async function refresh() {
    if (refreshing.value) return

    refreshing.value = true

    try {
        const response = await get(`files/${route.params.id}`)

        file.value = {
            ...response.file,

            // Folder is ignored in backend json. Keep the previous value.
            Folder: file.value.Folder,
        }
    } finally {
        refreshing.value = false
    }

    refreshTimer = setTimeout(refresh, 5000)
}
</script>
