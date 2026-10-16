<template>
    <list-link
        :id="folder.Id"
        :name="folder.Name"
        :path="folder.Path"
        :archived="folder.Archived"
        :restorable="restorable"
        :current-icon="faFolderOpen"
        :archived-icon="faFolderClosed"
        route-prefix="folders"
        hide-system-name
        hide-restore-button
        @destroy="$emit('destroy')"
        @restore="$emit('restore')"
        @rename="$emit('rename', $event)"
    />
</template>

<script setup>
import { faFolderClosed, faFolderOpen } from '@fortawesome/free-solid-svg-icons'
import ListLink from './list-link.vue'

defineProps({
    folder: {
        type: Object,
        required: true,
    },
    restorable: Boolean,
})

defineEmits(['destroy', 'restore', 'rename'])
</script>
