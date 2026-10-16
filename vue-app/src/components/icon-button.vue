<template>
    <component
        :is="disabled ? 'button' : 'router-link'"
        :to="to"
        :disabled="disabled"
        class="h-10 flex gap-1 items-center justify-center shrink-0 border
            rounded"
        :class="[
            $slots.default ? 'px-2' : 'w-10',
            disabled ? 'opacity-25' : '',
        ]"
        @click="_click"
    >
        <font-awesome-icon :icon="icon" :spin="spin" :class="iconClass" />
        <slot />
    </component>
</template>

<script setup>
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'

const props = defineProps({
    to: {
        type: String,
        default: '',
    },
    icon: {
        type: Object,
        required: true,
    },
    spin: Boolean,
    disabled: Boolean,
    iconClass: {
        type: [String, Array],
        default: '',
    },
})

const emit = defineEmits(['click'])

function _click() {
    if (!props.disabled) emit('click')
}
</script>
