export async function apiFetch(path: string) {
    let value = null

    try {
        const response = await fetch(`http://localhost:5001/api/${path}`)

        if (!response.ok) {
            throw new Error('Network error targeting local server')
        }

        value = await response.json()
    } catch (error) {
        console.error('Could not reach backend server:', error)
    }

    return value
}
