<template>
    <list-link
        :id="file.Id"
        :name="file.Name"
        :path="file.Path"
        :archived="file.Archived"
        :restorable="restorable"
        :current-icon="faFileLines"
        :archived-icon="faFileZipper"
        route-prefix="files"
        @destroy="$emit('destroy')"
        @restore="$emit('restore')"
        @rename="$emit('rename', $event)"
    />
</template>

<script setup>
import { faFileLines, faFileZipper } from '@fortawesome/free-solid-svg-icons'
import ListLink from './list-link.vue'

defineProps({
    file: {
        type: Object,
        required: true,
    },
    restorable: Boolean,
})

defineEmits(['destroy', 'restore', 'rename'])
</script>
