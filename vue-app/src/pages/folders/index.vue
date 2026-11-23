<template>
    <layout>
        <template #header-left>
            <icon-button
                v-if="mode == 'archived'"
                :icon="faCircleLeft"
                icon-class="text-2xl text-blue-800"
                @click="mode = 'tracked'"
            />

            <icon-button
                v-else
                to="/about"
                :icon="faCircleInfo"
                icon-class="text-2xl text-blue-800"
            />

            <div class="px-1 text-2xl">{{ headerText }}</div>
        </template>

        <template #header-right>
            <font-awesome-icon
                v-if="mode == 'archived'"
                :icon="faBoxOpen"
                class="text-3xl text-gray-600"
            />

            <template v-else>
                <icon-button
                    v-if="archivedFolders.length"
                    :icon="faBoxOpen"
                    icon-class="text-xl text-gray-600"
                    @click="mode = 'archived'"
                >
                    Show Archived Folders
                </icon-button>

                <icon-button
                    :icon="faFolderPlus"
                    icon-class="text-2xl text-blue-800"
                    @click="newFolder"
                >
                    Track folder
                </icon-button>
            </template>
        </template>

        <template v-if="folders == null">
            <load-indicator
                v-if="longLoad"
                :icon="faFolder"
                message="Loading folders..."
                flip
                class="m-auto"
            />
        </template>

        <template v-else-if="mode == 'archived'">
            <folder-link-scroller
                v-if="archivedFolders.length"
                :folders="archivedFolders"
                @destroy="_destroy"
                @restore="_restore"
                @rename="_rename"
            />
        </template>

        <template v-else>
            <folder-link-scroller
                v-if="trackedFolders.length"
                :folders="trackedFolders"
                @archive="_archive"
                @rename="_rename"
            />

            <button v-else @click="newFolder" class="w-full h-full">
                <load-indicator
                    :icon="faFolderPlus"
                    message="Tap to track a folder"
                />
            </button>
        </template>
    </layout>
</template>

<script setup>
import { faFolder } from '@fortawesome/free-regular-svg-icons'
import {
    faBoxOpen,
    faCircleInfo,
    faCircleLeft,
    faFolderPlus,
} from '@fortawesome/free-solid-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import _ from 'lodash'
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import FolderLinkScroller from '@/components/folder-link-scroller.vue'
import IconButton from '@/components/icon-button.vue'
import LoadIndicator from '@/components/load-indicator.vue'
import Layout from '@/layouts/layout.vue'
import { get, post } from '@/util/helpers'
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css'

const route = useRoute()
const folders = ref(null)
const refreshing = ref(false)
const mode = ref(route.query.mode ?? 'tracked')
const longLoad = ref(false)

const trackedFolders = computed(() => {
    return _.filter(folders.value, { Archived: false })
})
const archivedFolders = computed(() => {
    return _.filter(folders.value, { Archived: true })
})

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

const headerText = computed(() => {
    return mode.value == 'archived' ? 'Archived Folders' : 'Tracked Folders'
})

async function newFolder() {
    const picked = await get('folders/pick')

    if (picked.cancelled) return

    await post('folders', { name: picked, path: picked })

    refresh()
}

async function refresh() {
    if (refreshing.value) return

    refreshing.value = true

    try {
        folders.value = await get('folders')
    } finally {
        refreshing.value = false
    }

    refreshTimer = setTimeout(refresh, 1000)
}

function _destroy(folder) {
    _.remove(folders.value, folder)

    if (archivedFolders.value.length == 0) mode.value = 'tracked'
}

function _archive(folder) {
    _.find(folders.value, folder).Archived = true
}

function _restore(folder) {
    _.find(folders.value, folder).Archived = false

    if (archivedFolders.value.length == 0) mode.value = 'tracked'
}

function _rename(folder, newName) {
    _.find(folders.value, folder).Name = newName
}
</script>
