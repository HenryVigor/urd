<template>
    <list-link-scroller v-slot="{ item: folder }" :list="folders">
        <list-link
            :id="folder.Id"
            :name="folder.Name"
            :path="folder.Path"
            :archived="folder.Archived"
            :restorable="folder.IsRestorable"
            :current-icon="faFolderOpen"
            :archived-icon="faFolderClosed"
            route-prefix="folders"
            :state="{
                folder: {
                    Id: folder.Id,
                    Name: folder.Name,
                    Path: folder.Path,
                    Archived: folder.Archived,
                },
            }"
            hide-system-name
            @destroy="$emit('destroy', folder)"
            @archive="$emit('archive', folder)"
            @restore="$emit('restore', folder)"
            @rename="$emit('rename', folder, $event)"
        />
    </list-link-scroller>
</template>

<script setup>
import { faFolderClosed, faFolderOpen } from '@fortawesome/free-solid-svg-icons'
import ListLinkScroller from './list-link-scroller.vue'
import ListLink from './list-link.vue'

defineProps({
    folders: {
        type: Array,
        required: true,
    },
})

defineEmits(['destroy', 'archive', 'restore', 'rename'])
</script>
