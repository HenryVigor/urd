<template>
    <layout>
        <template #header-left>
            <div class="flex gap-2 items-center">
                <icon-button
                    to="/folders"
                    :icon="faCircleLeft"
                    icon-class="text-2xl text-blue-800"
                />
            </div>

            <div class="px-2 text-2xl">About</div>
        </template>

        <template #header-right>
            <div class="text-sm">Copyright © 2026 Henry Vigor</div>
        </template>
    </layout>
</template>

<script setup>
import { faCircleLeft } from '@fortawesome/free-solid-svg-icons'
import IconButton from '@/components/icon-button.vue'
import Layout from '@/layouts/layout.vue'
</script>
