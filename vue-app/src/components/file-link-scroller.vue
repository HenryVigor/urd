<!-- TODO: Mark restorability here and in backend -->
<template>
    <list-link-scroller v-slot="{ item: file }" :list="files">
        <list-link
            :id="file.Id"
            :name="file.Name"
            :path="file.Path"
            :archived="file.Archived"
            :current-icon="faFileLines"
            :archived-icon="faFileZipper"
            route-prefix="files"
            hide-archive-button
            :archive-only="archiveOnly"
            @destroy="$emit('destroy', file)"
            @restore="$emit('restore', file)"
            @rename="$emit('rename', file, $event)"
        />
    </list-link-scroller>
</template>

<script setup>
import { faFileLines, faFileZipper } from '@fortawesome/free-solid-svg-icons';
import ListLinkScroller from './list-link-scroller.vue';
import ListLink from './list-link.vue';

defineProps({
    files: {
        type: Array,
        required: true,
    },
    archiveOnly: Boolean,
})

defineEmits(['destroy', 'restore', 'rename'])
</script>
