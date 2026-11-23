<template>
    <layout>
        <template #header-left>
            <icon-button
                v-if="mode == 'tracked' || folder.Archived"
                :to="{
                    path: '/folders',
                    query: { mode: folder.Archived ? 'archived' : 'tracked' },
                }"
                :icon="faCircleLeft"
                icon-class="text-2xl text-blue-800"
            />

            <icon-button
                v-else
                :icon="faCircleLeft"
                icon-class="text-2xl text-blue-800"
                @click="mode = 'tracked'"
            />

            <div class="px-1">
                <div class="text-xl">{{ folder?.Name }}</div>
                <div class="text-xs text-gray-600">{{ folder?.Path }}</div>
            </div>
        </template>

        <template #header-right>
            <font-awesome-icon
                v-if="mode == 'archived'"
                :icon="faBoxOpen"
                class="text-3xl text-gray-600"
            />

            <icon-button
                v-else-if="archivedFiles.length"
                :icon="faBoxOpen"
                icon-class="text-xl text-gray-600"
                @click="mode = 'archived'"
            >
                Show Archived Files
            </icon-button>
        </template>

        <template v-if="folder?.Files == null">
            <load-indicator
                v-if="longLoad"
                :icon="faFile"
                message="Loading files..."
                flip
                class="m-auto"
            />
        </template>

        <template v-else-if="mode == 'archived'">
            <file-link-scroller
                v-if="archivedFiles.length"
                :files="archivedFiles"
                :folder="folder"
                @destroy="_destroy"
                @restore="_restore"
                @rename="_rename"
            />

            <load-indicator
                v-else
                :icon="faFileCircleXmark"
                message="This folder has no archived files"
                class="m-auto"
            />
        </template>

        <template v-else>
            <file-link-scroller
                v-if="trackedFiles.length"
                :files="trackedFiles"
                :folder="folder"
                @rename="_rename"
            />

            <load-indicator
                v-else
                :icon="faFileCircleXmark"
                message="This folder has no tracked files"
                class="m-auto"
            />
        </template>
    </layout>
</template>

<script setup>
import { faFile } from '@fortawesome/free-regular-svg-icons'
import {
    faBoxOpen,
    faCircleLeft,
    faFileCircleXmark,
} from '@fortawesome/free-solid-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import _ from 'lodash'
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import FileLinkScroller from '@/components/file-link-scroller.vue'
import IconButton from '@/components/icon-button.vue'
import LoadIndicator from '@/components/load-indicator.vue'
import Layout from '@/layouts/layout.vue'
import { get } from '@/util/helpers'
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css'

const route = useRoute()
const folderId = route.params.id

const cachedFolder = window.history.state?.folder
const folder = ref(cachedFolder?.Id === Number(folderId) ? cachedFolder : null)
const refreshing = ref(false)
const longLoad = ref(false)

let refreshTimer = null

const trackedFiles = computed(() => {
    return _.filter(folder.value?.Files, { Archived: false })
})

const archivedFiles = computed(() => {
    return _.filter(folder.value?.Files, { Archived: true })
})

const initialMode = computed(() => {
    if (route.query.mode) return route.query.mode

    return folder.value?.Archived ? 'archived' : 'tracked'
})

const mode = ref(initialMode.value)

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

async function refresh() {
    if (refreshing.value) return

    refreshing.value = true

    try {
        const response = await get(`folders/${folderId}`)
        folder.value = response.folder
    } finally {
        refreshing.value = false
    }

    refreshTimer = setTimeout(refresh, 1000)
}

function _destroy(file) {
    _.remove(folder.value.Files, file)

    if (archivedFiles.value.length == 0) mode.value = 'tracked'
}

function _restore(file) {
    _.find(folder.value.Files, file).Archived = false

    if (archivedFiles.value.length == 0) mode.value = 'tracked'
}

function _rename(file, newName) {
    _.find(folder.value.Files, file).Name = newName
}
</script>
