<!-- TODO: Ensure updating indicators show up on correct list links when list changes -->
<!-- TODO: Keep folder in list after restoring until all files are also restored -->
<!-- TODO: Remove systemName -->
<template>
    <div class="h-10">
        <!-- Delete mode -->
        <list-link-mode
            v-if="mode == 'delete'"
            @cancel="mode = ''"
            @confirm="destroy"
        >
            <!-- TODO: Marquee here? -->
            <div
                class="w-full text-lg font-bold flex justify-center items-center
                    border rounded"
            >
                Permanently delete {{ name }}?
            </div>
        </list-link-mode>

        <!-- Archive Mode -->
        <list-link-mode
            v-else-if="mode == 'archive'"
            @cancel="mode = ''"
            @confirm="archive"
        >
            <!-- TODO: Marquee here? -->
            <div
                class="w-full text-lg flex justify-center items-center border
                    rounded"
            >
                Archive {{ name }}?
            </div>
        </list-link-mode>

        <!-- Restore mode -->
        <list-link-mode
            v-else-if="mode == 'restore'"
            @cancel="mode = ''"
            @confirm="restore"
        >
            <!-- TODO: Marquee here? -->
            <div
                class="w-full text-lg flex justify-center items-center border
                    rounded"
            >
                Restore {{ name }}?
            </div>
        </list-link-mode>

        <!-- Edit mode -->
        <list-link-mode
            v-else-if="mode == 'edit'"
            @cancel="mode = ''"
            @confirm="rename"
        >
            <input
                ref="nameInput"
                v-model="form.name"
                placeholder="New name"
                class="w-full px-4 border rounded"
                @keyup.enter="rename"
            />
        </list-link-mode>

        <!-- Default mode -->
        <div v-else class="h-full flex gap-1">
            <icon-button
                v-if="archived"
                :icon="destroying ? faSpinner : faTrashCan"
                :disabled="updating"
                :spin="destroying"
                :icon-class="[
                    'text-xl',
                    destroying ? 'text-gray-500' : 'text-red-600',
                ]"
                @click="mode = 'delete'"
            />

            <icon-button
                v-if="!archived && !hideArchiveButton"
                :icon="archiving ? faSpinner : faBoxOpen"
                :disabled="updating"
                :spin="archiving"
                :icon-class="[
                    'text-xl',
                    restoring ? 'text-gray-500' : 'text-gray-600',
                ]"
                @click="mode = 'archive'"
            />

            <icon-button
                v-if="archived && !hideRestoreButton"
                :icon="restoring ? faSpinner : faClockRotateLeft"
                :disabled="!restorable || updating"
                :spin="restoring"
                :icon-class="[
                    'text-xl',
                    restoring ? 'text-gray-500' : 'text-green-700',
                ]"
                @click="mode = 'restore'"
            />

            <icon-button
                :icon="renaming ? faSpinner : faPencilAlt"
                :disabled="updating"
                :spin="renaming"
                :icon-class="[
                    'text-xl',
                    renaming ? 'text-gray-500' : 'text-yellow-500',
                ]"
                @click="mode = 'edit'"
            />

            <router-link
                :to="{ path: `/${routePrefix}/${id}`, state: state }"
                class="w-full px-1 border rounded flex items-center
                    justify-between"
            >
                <!-- TODO: Marquee here -->
                <div class="flex items-center gap-4">
                    <font-awesome-icon
                        :icon="archived ? archivedIcon : currentIcon"
                        class="text-2xl"
                        :class="archived ? 'text-gray-600' : 'text-blue-800'"
                    />

                    <div class="text-lg font-semibold">{{ name }}</div>
                </div>

                <div class="flex items-center gap-8">
                    <div v-if="!hideSystemName" :class="systemNameColor">
                        {{ systemName }}
                    </div>

                    <font-awesome-icon
                        :icon="faCircleRight"
                        class="text-2xl text-blue-800"
                    />
                </div>
            </router-link>
        </div>
    </div>
</template>

<script setup>
import IconButton from '@/components/icon-button.vue'
import listLinkMode from '@/components/list-link-mode.vue'
import { del, patch, post } from '@/util/helpers'
import {
    faBoxOpen,
    faCircleRight,
    faClockRotateLeft,
    faPencilAlt,
    faSpinner,
    faTrashCan,
} from '@fortawesome/free-solid-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import { computed, ref, watch } from 'vue'

const props = defineProps({
    id: {
        type: Number,
        required: true,
    },
    name: {
        type: String,
        required: true,
    },
    path: {
        type: String,
        required: true,
    },
    currentIcon: {
        type: Object,
        required: true,
    },
    archivedIcon: {
        type: Object,
        required: true,
    },
    routePrefix: {
        type: String,
        required: true,
    },
    state: {
        type: Object,
        default: () => {},
    },
    archived: Boolean,
    restorable: Boolean,
    hideSystemName: Boolean,
    hideArchiveButton: Boolean,
    hideRestoreButton: Boolean,
})

const emit = defineEmits(['destroy', 'archive', 'restore', 'rename'])

const mode = ref('')

const nameInput = ref(null)

const destroying = ref(false)
const archiving = ref(false)
const restoring = ref(false)
const renaming = ref(false)

watch(
    () => props.id,
    () => {
        mode.value = ''
        form.value.name = props.name
    },
)

watch(
    mode,
    () => {
        if (mode.value == 'edit') {
            form.value.name = props.name
            nameInput.value?.focus()
        }
    },
    { flush: 'post' },
)

const form = ref({
    name: props.name,
})

const systemName = computed(() => {
    return props.path.split('/').pop()
})

const systemNameColor = computed(() => {
    return !props.archived || props.restorable
        ? 'text-gray-600'
        : 'text-red-400'
})

const updating = computed(() => {
    return (
        destroying.value || archiving.value || restoring.value || renaming.value
    )
})

async function destroy() {
    if (updating.value) return

    mode.value = ''
    destroying.value = true

    try {
        await del(`${props.routePrefix}/${props.id}`)

        emit('destroy')
    } catch (e) {
        console.error(`An error occurred while deleting: ${e}`)
    } finally {
        destroying.value = false
    }
}

async function archive() {
    if (updating.value) return

    mode.value = ''
    archiving.value = true

    try {
        await post(`${props.routePrefix}/${props.id}/archive`)

        emit('archive')
    } catch (e) {
        console.error(`An error occurred while restoring: ${e}`)
    } finally {
        archiving.value = false
    }
}

async function restore() {
    if (updating.value) return

    mode.value = ''
    restoring.value = true

    try {
        await post(`${props.routePrefix}/${props.id}/restore`)

        emit('restore')
    } catch (e) {
        console.error(`An error occurred while restoring: ${e}`)
    } finally {
        restoring.value = false
    }
}

async function rename() {
    if (updating.value) return

    mode.value = ''
    renaming.value = true

    try {
        const data = { name: form.value.name }
        await patch(`${props.routePrefix}/${props.id}/rename`, data)

        emit('rename', form.value.name)
    } catch (e) {
        console.error(`An error occurred while renaming: ${e}`)
    } finally {
        renaming.value = false
    }
}
</script>
