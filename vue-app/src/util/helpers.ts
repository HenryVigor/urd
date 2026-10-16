export async function get(path: string) {
    const response = await fetch(`http://localhost:5001/api/${path}`, {
        headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`)
    }

    return response.json()
}

export async function post(path: string, body: object) {
    const response = await fetch(`http://localhost:5001/api/${path}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    })

    if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`)
    }

    return response.json()
}
