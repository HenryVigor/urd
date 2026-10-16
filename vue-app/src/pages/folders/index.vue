<template>
    <!-- TODO: Implement service start/stop feature -->

    <div class="h-screen flex flex-col">
        <div
            class="px-2 py-3 flex gap-1 justify-between items-center rounded-b
                shadow"
        >
            <div class="flex gap-1 items-center shrink-0">
                <icon-button
                    :icon="faPlus"
                    icon-class="text-2xl text-green-700"
                    @click="newFolder"
                />
                <div class="px-1 text-2xl">
                    {{ showArchived ? 'Archived Folders' : 'Tracked Folders' }}
                </div>
            </div>

            <icon-button
                v-if="archivedFolders.length"
                :icon="showArchived ? faFolderOpen : faFolderClosed"
                :icon-class="[
                    'text-xl',
                    showArchived ? 'text-blue-800' : 'text-gray-600',
                ]"
                @click="showArchived = !showArchived"
            >
                {{ archiveToggleButtonText }}
            </icon-button>
        </div>

        <div
            class="min-h-0 p-2 flex flex-col flex-1 space-y-1 text-center
                items-center overflow-y-auto overscroll-contain"
        >
            <template v-if="showArchived">
                <folder-link
                    v-for="folder in archivedFolders"
                    :key="folder.Id"
                    :folder="folder"
                    :restorable="isRestorable(folder)"
                    class="w-full"
                    @destroy="_destroy(folder)"
                    @restore="_restore(folder)"
                    @rename="_rename(folder, $event)"
                />

                <div
                    v-if="!archivedFolders.length"
                    class="h-full text-gray-400"
                >
                    There are no archived folders
                </div>
            </template>

            <template v-else>
                <folder-link
                    v-for="folder in currentFolders"
                    :key="folder.Id"
                    :folder="folder"
                    class="w-full"
                    @rename="_rename(folder, $event)"
                />

                <div
                    v-if="!currentFolders.length"
                    class="my-auto text-gray-400"
                >
                    Not tracking any folders
                </div>
            </template>
        </div>
    </div>
</template>

<script setup>
import {
    faFolderClosed,
    faFolderOpen,
    faPlus,
} from '@fortawesome/free-solid-svg-icons'
import _ from 'lodash'
import { computed, onMounted, ref } from 'vue'
import FolderLink from '@/components/folder-link.vue'
import IconButton from '@/components/icon-button.vue'
import { get, post } from '@/util/helpers'

const folders = ref([])
const showArchived = ref(false)
const refreshing = ref(false)

const currentFolders = computed(() => {
    return _.filter(folders.value, { Archived: false })
})
const archivedFolders = computed(() => {
    return _.filter(folders.value, { Archived: true })
})

onMounted(async () => {
    refresh()
})

const archiveToggleButtonText = computed(() => {
    return showArchived.value ? 'Show Current Folders' : 'Show Archived Folders'
})

async function newFolder() {
    const picked = await get('folders/pick')

    if (picked.cancelled) return

    await post('folders', { name: picked, path: picked })

    refresh()
}

// TODO: Investigate duplicate refresh loops (two while open, one while backgrounded)
async function refresh() {
    if (refreshing.value) return

    refreshing.value = true

    try {
        folders.value = await get('folders')
    } finally {
        refreshing.value = false
    }

    setTimeout(refresh, 1000)
}

function isRestorable(folder) {
    const inArchive =
        _.findIndex(currentFolders.value, { Path: folder.Path }) == -1

    return folder.Archived && inArchive
}

function _destroy(folder) {
    _.remove(folders.value, folder)
    showArchived.value = archivedFolders.value.length > 0
}

function _restore(folder) {
    _.find(folders.value, folder).Archived = false
    showArchived.value = archivedFolders.value.length > 0
}

function _rename(folder, newName) {
    _.find(folders.value, folder).Name = newName
}
</script>
