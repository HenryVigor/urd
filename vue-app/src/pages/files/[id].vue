<template>
    <div class="flex min-h-dvh flex-col">
        <router-link
            :to="`/folders/${file?.FolderId}`"
            class="w-fit font-bold border rounded"
        >
            Back to folder
        </router-link>

        <div class="grid gap-4 p-4">
            <div v-for="row in diffsGrid" :key="row.key" class="flex gap-4">
                <div
                    v-for="(diff, i) in row.diffs"
                    :key="diff?.Id ?? `empty-${i}`"
                    class="w-20"
                >
                    <diff
                        v-if="diff"
                        :ref="(el) => setDiffRef(el, diff.Id)"
                        :diff="diff"
                        :head="diff.Id == file.HeadDiffId"
                        class="w-full border rounded"
                    />
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import _ from 'lodash'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import LeaderLine from 'vue3-leaderline'
import Diff from '@/components/diff.vue'
import { get } from '@/util/helpers'

const route = useRoute()

const file = ref(null)
const diffs = ref([])
const branches = ref([])
const lines = []

const diffRefs = new Map()

onMounted(async () => {
    await refresh()
})

onUnmounted(() => {
    eraseLines()
})

const diffsGrid = computed(() => {
    if (diffs.value.length == 0) return []

    let data = mapDiffsToBranchDataObjects()
    data = getSortableBranchData(data)
    data = sortBranches(data)
    data = positionBranches(data)

    return data
})

watch(diffsGrid, () => drawLines(), { flush: 'post' })

function mapDiffsToBranchDataObjects() {
    const diffsById = new Map(diffs.value.map((diff) => [diff.Id, diff]))

    return branches.value.map((branch) => {
        const branchDiffs = []
        let next = branch.TipDiffId

        while (next != null) {
            const diff = diffsById.get(next)

            branchDiffs.push(diff)

            if (diff.Id == branch.FirstDiffId) break

            next = diff.ParentId
        }

        return {
            id: branch.Id,
            diffs: branchDiffs.reverse(),
            offset: 0,
            parentBranch: -1,
            generation: 1,
        }
    })
}

function getSortableBranchData(data) {
    for (let i = 1; i < data.length; i++) {
        // Each row starting with the second
        const parentId = data[i].diffs[0].ParentId

        for (let ii = i - 1; ii >= 0; ii--) {
            // Each previous row
            const parentNodeX = _.findIndex(
                data[ii].diffs,
                (diff) => diff.Id == parentId,
            )

            if (parentNodeX > -1) {
                data[i].offset = parentNodeX + data[ii].offset + 1
                data[i].parentBranch = data[ii].id
                data[i].generation = data[ii].generation + 1
                break
            }
        }
    }

    return data
}

function sortBranches(data) {
    let sorted = []

    let gen = _.maxBy(data, (branch) => branch.generation).generation

    for (; gen > 0; gen--) {
        const next = _.filter(data, { generation: gen })

        // Add parents to previous sibling groups
        sorted = sorted.map((group) => {
            return [_.remove(next, { id: group[0].parentBranch })[0], ...group]
        })

        // Add childless siblings of those parents to outer layer
        sorted = [...sorted, ...next.map((branch) => [branch])]

        // Group by next parent
        sorted = Object.values(
            _.groupBy(sorted, (group) => group[0].parentBranch),
        )

        // Order by horizontal offset and flatten for next pass
        sorted = sorted.map((group) => {
            return _.flatten(
                _.orderBy(group, (branch) => branch[0].offset, 'desc'),
            )
        })
    }

    return _.flatten(sorted)
}

function positionBranches(data) {
    // Apply horizontal offsets and initialize branch id list for each row
    data.forEach((row) => {
        row.ids = [row.id]
        row.diffs = [...Array(row.offset).fill(null), ...row.diffs]
    })

    // Insert spacers where space should be left for drawn lines
    for (let i = 1; i < data.length; i++) {
        // Each row starting with the second
        const lineX = data[i].offset - 1

        for (let ii = i; data[ii].id != data[i].parentBranch && i > 0; ii--) {
            // This row up until parent
            const diffs = data[ii].diffs

            if (diffs[lineX] == null) {
                diffs[lineX] = { connections: 1, spacer: true }
            } else diffs[lineX].connections++
        }
    }

    // Move branches up to the highest row where there is space
    for (let i = 2; i < data.length; i++) {
        // Each row starting with the third
        // Determine how far upwards this branch can move
        const start = data[i].offset
        const end = data[i].diffs.length - 1
        let newRow = i

        for (
            let ii = i - 1;
            !data[ii].ids.includes(data[i].parentBranch) && ii > 0;
            ii--
        ) {
            // Up until parent
            for (let j = start; j <= end; j++) {
                // Each cell directly above
                if (data[ii].diffs[j] != null) break

                if (j < end) continue

                newRow = ii
            }
        }

        if (newRow == i) continue

        // Move branch
        for (let j = start; j <= end; j++) {
            data[newRow].diffs[j] = data[i].diffs[j]
        }

        data[newRow].ids.push(data[i].id)
        data.splice(i--, 1)

        // Handle spacers that no longer belong to this branch
        for (let ii = i; ii > newRow; ii--) {
            const diffs = data[ii].diffs
            const spacerX = start - 1

            if (diffs[spacerX].connections > 1) diffs[spacerX].connections--
            else diffs[spacerX] = null
        }

        // Extend spacers that belong to child branches
        for (let j = start; j <= end; j++) {
            const nextRowDiffs = data[i + 1].diffs

            if (nextRowDiffs[j]?.spacer) {
                for (let ii = i; ii > newRow; ii--) {
                    data[ii].diffs[j] = {
                        connections: nextRowDiffs[j].connections,
                        spacer: true,
                    }
                }
            }
        }
    }

    // Remove spacers and map data to keys and diff arrays only
    return data.map((row) => ({
        key: row.ids[0],
        diffs: _.map(row.diffs, (item) => (item?.spacer ? null : item)),
    }))
}

function setDiffRef(el, id) {
    if (el) diffRefs.set(id, el)
    else diffRefs.delete(id)
}

async function refresh() {
    const response = await get(`files/${route.params.id}`)
    file.value = response.file
    diffs.value = response.diffs
    branches.value = response.branches
}

function drawLines() {
    eraseLines()

    diffs.value.forEach((diff) => {
        const endEl = diffRefs.get(diff.Id)?.$el
        const startEl = diffRefs.get(diff.ParentId)?.$el

        if (!(endEl && startEl)) return

        const startOfBranch =
            _.findIndex(branches.value, { FirstDiffId: diff.Id }) > -1

        lines.push(
            new LeaderLine({
                end: endEl,
                start: startEl,

                size: 3,
                color: 'black',
                path: startOfBranch ? 'fluid' : 'straight',
                startSocket: startOfBranch ? 'bottom' : 'right',
                endSocket: 'left',
                startSocketGravity: 30,
                endSocketGravity: 55,

                startPlug: 'behind',
                endPlug: 'behind',
            }),
        )
    })
}

function eraseLines() {
    lines.forEach((line) => line.remove())
    lines.length = 0
}
</script>
