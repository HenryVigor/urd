<template>
    <div class="h-screen flex flex-col">
        <div
            class="h-15 p-2 flex gap-1 justify-between items-center rounded-b
                shadow"
        >
            <div class="flex gap-1 items-center">
                <slot name="header-left" />
            </div>

            <div class="flex gap-1 items-center">
                <slot name="header-right" />
            </div>
        </div>

        <div class="min-h-0 p-2 flex grow">
            <slot />
        </div>
    </div>
</template>
