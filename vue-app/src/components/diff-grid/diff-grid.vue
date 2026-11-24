<template>
    <div ref="scrollEl" class="h-full overflow-auto">
        <div
            class="relative"
            :style="{
                width: `${colVirtualizer.getTotalSize()}px`,
                height: `${rowVirtualizer.getTotalSize()}px`,
            }"
        >
            <!-- Lines between diff nodes -->
            <svg
                class="absolute top-0 left-0 pointer-events-none"
                :width="colVirtualizer.getTotalSize()"
                :height="rowVirtualizer.getTotalSize()"
            >
                <path
                    v-for="edge in visibleEdges"
                    :key="edge.key"
                    :d="edgePath(edge)"
                    fill="none"
                    stroke="black"
                    stroke-width="3"
                />
            </svg>

            <!-- Diff cells -->
            <template v-for="cell in visibleCells" :key="cell.key">
                <!-- <marquee
                    v-if="cell.branchName"
                    :text="cell.branchName"
                    class="absolute top-0 left-0 text-xs font-bold italic"
                    :style="cell.branchStyle"
                >
                    {{ cell.branchName }}
                </marquee> -->

                <div class="absolute -top-3 left-0" :style="cell.dateStyle">{{ cell.branchLength }}, {{ cell.branchTrailingSpace }}</div>

                <date
                    v-if="cell.showDate"
                    :date="cell.diff.CreatedAt"
                    class="absolute top-0 left-0 text-center"
                    :style="cell.dateStyle"
                />

                <diff-node
                    :diff="cell.diff"
                    :size="diffSize"
                    :head="cell.diff.Id == headDiffId"
                    class="absolute top-0 left-0"
                    :style="cell.nodeStyle"
                />
            </template>
        </div>
    </div>
</template>

<script setup>
import { useVirtualizer } from '@tanstack/vue-virtual'
import _ from 'lodash'
import moment from 'moment'
import { computed, ref } from 'vue'
import Date from './date.vue'
import DiffNode from './diff-node.vue'

const props = defineProps({
    diffs: {
        type: Array,
        required: true,
    },
    branches: {
        type: Array,
        required: true,
    },
    headDiffId: {
        type: [Number, null],
        required: true,
    },
})

const diffSize = 40 // Diameter of visual diff node
const cellWidth = 40 // Width of full diff cell
const cellHeight = 50 // Height of full diff cell
const cellGap = 16 // Space between cells
const cellStrideX = cellWidth + cellGap
const cellStrideY = cellHeight + cellGap

const scrollEl = ref(null)

const diffsArray = computed(() => {
    if (!props.diffs.length || !props.branches.length) return []

    let data = mapDiffsToBranchDataObjects()
    data = getSortableBranchData(data)
    data = sortBranches(data)
    data = positionBranches(data)

    return data
})

const graph = computed(() => {
    const grid = diffsArray.value
    const cells = new Map()

    grid.forEach((row, r) =>
        row.diffs.forEach((item, c) => {
            if (!item?.diff) return

            const diff = item.diff

            cells.set(diff.Id, { diff, r, c })
        }),
    )

    const branchStarts = new Set(
        Object.values(props.branches ?? {}).map((b) => b.FirstDiffId),
    )

    const edges = []

    for (const { diff, r, c } of cells.values()) {
        const parent = cells.get(diff.ParentId)

        if (!parent) continue

        edges.push({
            key: diff.Id,
            from: parent,
            to: { r, c },
            branchStart: branchStarts.has(diff.Id),
        })
    }

    const columnCount = _.max(grid.map((row) => row.diffs.length)) ?? 0

    return { edges, columnCount }
})

const rowVirtualizer = useVirtualizer(
    computed(() => ({
        count: diffsArray.value.length,
        getScrollElement: () => scrollEl.value,
        estimateSize: () => cellHeight,
        gap: cellGap,
        overscan: 5,
    })),
)

const colVirtualizer = useVirtualizer(
    computed(() => ({
        horizontal: true,
        count: graph.value.columnCount,
        getScrollElement: () => scrollEl.value,
        estimateSize: () => cellWidth,
        gap: cellGap,
        overscan: 5,
    })),
)

const virtualRows = computed(() => rowVirtualizer.value.getVirtualItems())
const virtualCols = computed(() => colVirtualizer.value.getVirtualItems())

const visibleCells = computed(() => {
    const cells = []

    for (const row of virtualRows.value) {
        for (const col of virtualCols.value) {
            const cell = cellAt(row.index, col.index)

            if (!cell) continue

            cells.push({
                ...cell,
                key: `${row.key}-${col.key}`,
                branchStyle: {
                    width: `${cell.branchSpace * cellStrideX}px`,
                    transform: `translate(${col.start}px, ${row.start - 27}px)`,
                },
                dateStyle: {
                    width: `${cellWidth}px`,
                    transform: `translate(${col.start}px, ${row.start - 15}px)`,
                },
                nodeStyle: {
                    transform: `translate(${col.start}px, ${row.start}px)`,
                },
            })
        }
    }

    return cells
})

const visibleEdges = computed(() => {
    const rows = virtualRows.value
    const cols = virtualCols.value

    if (!rows.length || !cols.length) return []

    const r0 = rows[0].index
    const r1 = rows.at(-1).index
    const c0 = cols[0].index
    const c1 = cols.at(-1).index

    return graph.value.edges.filter(({ from, to }) => {
        return (
            Math.max(from.r, to.r) >= r0 &&
            Math.min(from.r, to.r) <= r1 &&
            Math.max(from.c, to.c) >= c0 &&
            Math.min(from.c, to.c) <= c1
        )
    })
})

function mapDiffsToBranchDataObjects() {
    const diffsById = new Map(
        Object.values(props.diffs).map((diff) => [diff.Id, diff]),
    )

    return Object.values(props.branches).map((branch) => {
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
            const nextRowDiffs = data[i + 1]?.diffs

            if (nextRowDiffs?.[j]?.spacer) {
                for (let ii = i; ii > newRow; ii--) {
                    data[ii].diffs[j] = {
                        connections: nextRowDiffs[j].connections,
                        spacer: true,
                    }
                }
            }
        }
    }

    // Map data
    return data.map((row) => ({
        key: row.ids[0],
        diffs: _.map(row.diffs, (diff, diffIndex) => {
            if (!diff || diff.spacer) return null

            const previousDiff = row.diffs[diffIndex - 1]

            const firstDiffInBranch = diffIndex == 0 || previousDiff?.spacer

            // Get branch info
            // TODO: Continue calculating here for spacer info, but move to separate array to reduce nulls
            let branchName = null
            let branchSpace = null // TODO: Remove this
            let branchLength = null
            let branchTrailingSpace = null

            if (firstDiffInBranch) {
                // Name
                branchName = _.find(props.branches, {
                    FirstDiffId: diff.Id,
                }).Name

                let i = diffIndex + 1

                // Length
                while (true) {
                    const cell = row.diffs[i]

                    if (!cell || cell.spacer) break

                    i++
                }

                branchLength = i - diffIndex

                // Empty space after
                for (; !row.diffs[i] && i < row.diffs.length; i++) {}

                branchTrailingSpace = i - branchLength - diffIndex


                // branchSpace = 0

                // while (true) {
                //     const next = diffIndex + ++branchSpace

                //     if (row.diffs[next]?.spacer) break

                //     if (row.diffs.length <= next - 2) {
                //         branchSpace = -1
                //         break
                //     }
                // }
            }

            // Get date info
            const showDate =
                firstDiffInBranch ||
                !moment(diff.CreatedAt).isSame(previousDiff.CreatedAt, 'day')

            return { diff, branchName, branchLength, branchTrailingSpace, showDate }
        }),
    }))
}

function cellAt(row, col) {
    return diffsArray.value[row]?.diffs[col]
}

function isHead(row, col) {
    return cellAt(row, col).diff.Id == props.headDiffId
}

function edgePath({ from, to, branchStart }) {
    const x2 = to.c * cellStrideX
    const y2 = to.r * cellStrideY + diffSize / 2

    if (!branchStart) {
        // Straight line from parent's right edge to child's left edge
        const x1 = from.c * cellStrideX + diffSize

        return `M ${x1} ${y2} H ${x2}`
    }

    // Curved line from parent's bottom edge to child's left edge
    const x1 = from.c * cellStrideX + diffSize / 2
    const y1 = from.r * cellStrideY + diffSize
    const r = Math.min(30, x2 - x1, y2 - y1)

    return `M ${x1} ${y1} V ${y2 - r} Q ${x1} ${y2} ${x1 + r} ${y2} H ${x2}`
}

function diffStyle(row, col) {
    return { transform: `translate(${col.start}px, ${row.start}px)` }
}
</script>
