<!-- TODO: Show archived on start if backing out of an archived file -->
<!-- TODO: Show archived on start if folder is archived -->
<!-- TODO: Add first load indicator -->
<template>
    <div class="h-screen flex flex-col">
        <div
            class="px-2 py-3 flex gap-1 justify-between items-center rounded-b
                shadow"
        >
            <div class="flex gap-1 items-center shrink-0">
                <icon-button
                    to="/folders"
                    :icon="faCircleArrowLeft"
                    icon-class="text-2xl text-blue-800"
                />
                <div class="-my-96 px-1">
                    <div class="text-xl">{{ folder?.Name }}</div>
                    <div class="text-xs text-gray-600">{{ folder?.Path }}</div>
                </div>
            </div>

            <icon-button
                v-if="archivedFiles.length"
                :icon="showArchived ? faFileLines : faFileZipper"
                :icon-class="[
                    'text-xl',
                    showArchived ? 'text-blue-800' : 'text-gray-600',
                ]"
                @click="toggleArchived"
            >
                {{ archiveToggleButtonText }}
            </icon-button>
        </div>

        <div
            class="min-h-0 p-2 flex flex-col flex-1 space-y-1 text-center
                items-center overflow-y-auto overscroll-contain"
        >
            <template v-if="showArchived">
                <file-link
                    v-for="file in archivedFiles"
                    :key="file.Id"
                    :file="file"
                    :restorable="isRestorable(file)"
                    class="w-full"
                    @destroy="_destroy(file)"
                    @restore="_restore(file)"
                    @rename="_rename(file, $event)"
                />

                <div v-if="!archivedFiles.length" class="h-full text-gray-400">
                    This folder has no archived files
                </div>
            </template>

            <template v-else>
                <file-link
                    v-for="file in currentFiles"
                    :key="file.Id"
                    :file="file"
                    class="w-full"
                    @rename="_rename(file, $event)"
                />

                <div v-if="!currentFiles.length" class="my-auto text-gray-400">
                    This folder has no tracked files
                </div>
            </template>
        </div>
    </div>
</template>

<script setup>
import { faFileLines, faFileZipper } from '@fortawesome/free-solid-svg-icons'
import { faCircleArrowLeft } from '@fortawesome/free-solid-svg-icons/faCircleArrowLeft'
import _ from 'lodash'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import FileLink from '@/components/file-link.vue'
import IconButton from '@/components/icon-button.vue'
import { get } from '@/util/helpers'

const route = useRoute()
const folderId = route.params.id

const folder = ref(null)
const files = ref([])
const showArchived = ref(false)
const refreshing = ref(false)

let refreshTimer = null

const currentFiles = computed(() => {
    return _.filter(folder.value?.Files, { Archived: false })
})
const archivedFiles = computed(() => {
    return _.filter(folder.value?.Files, { Archived: true })
})

onMounted(() => {
    refresh()
})

onUnmounted(() => {
    clearTimeout(refreshTimer)
})

const archiveToggleButtonText = computed(() => {
    return showArchived.value ? 'Show Current Files' : 'Show Archived Files'
})

async function refresh() {
    if (refreshing.value) return

    refreshing.value = true

    try {
        const response = await get(`folders/${folderId}`)
        folder.value = response.folder
        files.value = folder.value.Files
    } finally {
        refreshing.value = false
    }

    refreshTimer = setTimeout(refresh, 1000)
}

function toggleArchived() {
    showArchived.value = !showArchived.value
}

function isRestorable(file) {
    const inArchive = _.findIndex(currentFiles.value, { Path: file.Path }) == -1

    return file.Archived && inArchive
}

function _destroy(file) {
    _.remove(files.value, file)
    showArchived.value = archivedFiles.value.length > 0
}

function _restore(file) {
    _.find(files.value, file).Archived = false
    showArchived.value = archivedFiles.value.length > 0
}

function _rename(file, newName) {
    _.find(files.value, file).Name = newName
}
</script>
